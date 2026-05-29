using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ConsoleMain
{
    internal static class WebSceneServer
    {
        internal static void Start()
        {
            const string prefix = "http://localhost:5055/";
            using HttpListener listener = new();
            listener.Prefixes.Add(prefix);
            listener.Start();

            Console.WriteLine($"Web service started: {prefix}");
            Console.WriteLine("Press Ctrl+C to stop.");

            try
            {
                Process.Start(new ProcessStartInfo(prefix)
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open browser failed: {ex.Message}");
            }

            while (listener.IsListening)
            {
                HttpListenerContext context = listener.GetContext();
                HandleWebRequest(context);
            }
        }

        private static void HandleWebRequest(HttpListenerContext context)
        {
            try
            {
                string requestPath = context.Request.Url?.AbsolutePath ?? "/";
                if (requestPath == "/" || requestPath.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWebResponse(context, "text/html", WebIndexHtml);
                    return;
                }

                if (requestPath.Equals("/api/scene", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWebResponse(context, "application/json", BuildWebSceneJson(context.Request));
                    return;
                }

                WriteWebResponse(context, "text/plain", "Not found.", 404);
            }
            catch (Exception ex)
            {
                string json = JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    stackTrace = ex.ToString(),
                });
                WriteWebResponse(context, "application/json", json, 500);
            }
        }

        private static string BuildWebSceneJson(HttpListenerRequest request)
        {
            int startSelector = ParseWebSelector(request.QueryString["start"], 0);
            int targetSelector = ParseWebSelector(request.QueryString["target"], 1);
            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);

            if (!File.Exists(circlePath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", circlePath);
            }

            List<CircleGenerator.CircleRecord> circles = ReadCircleRecordsForWeb(circlePath);
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(circlePath);
            IReadOnlyList<CircleGenerator.DerivedPoint> points = pointData.Points
                .OrderBy(item => item.CircleIndex)
                .ThenBy(item => item.PointIndex)
                .ThenBy(item => item.PointType)
                .ToList();
            CircleGenerator.ValidateCudaPointOrderAndUniqueness(points);
            CircleGenerator.ValidateCudaPointBusinessRules(points, pointData.PathPointCounts);

            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);
            int[] connect = CircleGenerator.BuildConnectArray(points, pointData.OrdinaryConnections);
            List<int> routePointIndexes = BuildWebRoutePointIndexes(
                circlePath,
                pointData,
                points,
                cudaPoints,
                startSelector,
                targetSelector,
                out string? routeError);

            object scene = new
            {
                circleFile = circlePath,
                routeFile = Program.BuildRouteFilePath(circlePath),
                startSelector,
                targetSelector,
                routeError,
                roleCodes = new
                {
                    harvest = CircleGenerator.HarvestPointCode,
                    first = CircleGenerator.FirstPathPointCode,
                    terminal = CircleGenerator.TerminalLinkPointCode,
                    ordinary = CircleGenerator.OrdinaryLinkPointCode,
                    purchase = CircleGenerator.PurchasePointCode,
                },
                circles = circles.Select((circle, index) => new
                {
                    index,
                    a = circle.A,
                    b = circle.B,
                    r = circle.SignedRadius,
                    radius = circle.Radius,
                    direction = circle.SignedRadius > 0 ? "long" : "short",
                    pathPointCount = pointData.PathPointCounts[index],
                }),
                points = points.Select((point, index) => BuildWebPointDto(
                    index,
                    point,
                    circles,
                    pointData.PathPointCounts)),
                ordinaryConnections = pointData.OrdinaryConnections.Select(connection => BuildWebOrdinaryConnectionDto(
                    connection,
                    circles,
                    pointData.PathPointCounts)),
                terminalConnections = circles.Select((circle, index) => BuildWebTerminalConnectionDto(
                    index,
                    circle,
                    pointData.PathPointCounts[index])),
                purchaseAssignments = pointData.PurchaseAssignments.Select(assignment => BuildWebPurchaseAssignmentDto(
                    assignment,
                    circles,
                    pointData.PathPointCounts)),
                connect = connect.Select((targetIndex, sourceIndex) => new
                {
                    sourceIndex,
                    targetIndex,
                }),
                route = new
                {
                    pointIndexes = routePointIndexes,
                    points = routePointIndexes.Select(index => BuildWebPointDto(
                        index,
                        points[index],
                        circles,
                        pointData.PathPointCounts)),
                },
            };

            return JsonSerializer.Serialize(
                scene,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
        }

        private static int ParseWebSelector(string? value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (!int.TryParse(value, out int selector))
            {
                throw new InvalidOperationException($"Invalid route selector: {value}.");
            }

            return selector;
        }

        private static List<int> BuildWebRoutePointIndexes(
            string circlePath,
            CircleGenerator.GeneratedPointData pointData,
            IReadOnlyList<CircleGenerator.DerivedPoint> points,
            int[] cudaPoints,
            int startSelector,
            int targetSelector,
            out string? routeError)
        {
            try
            {
                int pointCount = cudaPoints.Length / 3;
                List<int> tradingPoints = Program.BuildTradingPointIndexes(cudaPoints);
                int harvestPointCount = Program.CountPointsByCode(points, CircleGenerator.HarvestPointCode);
                int purchasePointCount = Program.CountPointsByCode(points, CircleGenerator.PurchasePointCode);
                CircleGenerator.ValidateTradingPoints(
                    cudaPoints,
                    tradingPoints,
                    harvestPointCount,
                    purchasePointCount);

                int startPointIndex = Program.ResolveRouteSelectorToCudaPointIndex(
                    startSelector,
                    points,
                    pointData.PurchaseAssignments);
                int targetPointIndex = Program.ResolveRouteSelectorToCudaPointIndex(
                    targetSelector,
                    points,
                    pointData.PurchaseAssignments);
                int startTradingRow = tradingPoints.IndexOf(startPointIndex);

                if (startTradingRow < 0)
                {
                    throw new InvalidOperationException(
                        $"Start selector {startSelector} resolved to CUDA point {startPointIndex}, but it is not a trading point.");
                }

                int[] routeRow = Program.ReadRouteRow(
                    Program.BuildRouteFilePath(circlePath),
                    startTradingRow,
                    tradingPoints.Count,
                    pointCount);
                routeError = null;
                return Program.BuildRoutePointIndexes(routeRow, startPointIndex, targetPointIndex);
            }
            catch (Exception ex)
            {
                routeError = ex.Message;
                return new List<int>();
            }
        }

        private static object BuildWebPointDto(
            int index,
            CircleGenerator.DerivedPoint point,
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts)
        {
            (double x, double y) = CalculateWebPointCoordinate(
                point.CircleIndex,
                point.PointIndex,
                circles,
                pathPointCounts);

            return new
            {
                index,
                circleIndex = point.CircleIndex,
                pointIndex = point.PointIndex,
                pointType = point.PointType,
                x,
                y,
            };
        }

        private static object BuildWebOrdinaryConnectionDto(
            CircleGenerator.ConnectionPair connection,
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts)
        {
            (double leftX, double leftY) = CalculateWebPointCoordinate(
                connection.LeftCircleIndex,
                connection.LeftPointIndex,
                circles,
                pathPointCounts);
            (double rightX, double rightY) = CalculateWebPointCoordinate(
                connection.RightCircleIndex,
                connection.RightPointIndex,
                circles,
                pathPointCounts);

            return new
            {
                leftCircleIndex = connection.LeftCircleIndex,
                leftPointIndex = connection.LeftPointIndex,
                rightCircleIndex = connection.RightCircleIndex,
                rightPointIndex = connection.RightPointIndex,
                leftX,
                leftY,
                rightX,
                rightY,
            };
        }

        private static object BuildWebTerminalConnectionDto(
            int circleIndex,
            CircleGenerator.CircleRecord circle,
            int pathPointCount)
        {
            (double sourceX, double sourceY) = CircleGenerator.CalculatePathPointCoordinate(
                circle,
                pathPointCount - 1,
                pathPointCount);
            (double targetX, double targetY) = CircleGenerator.CalculatePathPointCoordinate(
                circle,
                0,
                pathPointCount);

            return new
            {
                circleIndex,
                sourcePointIndex = pathPointCount - 1,
                targetPointIndex = 0,
                sourceX,
                sourceY,
                targetX,
                targetY,
            };
        }

        private static object BuildWebPurchaseAssignmentDto(
            CircleGenerator.PurchaseAssignment assignment,
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts)
        {
            CircleGenerator.CircleRecord sourceCircle = circles[assignment.CircleIndex];
            (double targetX, double targetY) = CalculateWebPointCoordinate(
                assignment.PurchaseCircleIndex,
                assignment.PurchasePointIndex,
                circles,
                pathPointCounts);

            return new
            {
                circleIndex = assignment.CircleIndex,
                sourceX = sourceCircle.A,
                sourceY = sourceCircle.B,
                purchaseCircleIndex = assignment.PurchaseCircleIndex,
                purchasePointIndex = assignment.PurchasePointIndex,
                targetX,
                targetY,
            };
        }

        private static (double X, double Y) CalculateWebPointCoordinate(
            int circleIndex,
            int pointIndex,
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts)
        {
            return CircleGenerator.CalculatePathPointCoordinate(
                circles[circleIndex],
                pointIndex,
                pathPointCounts[circleIndex]);
        }

        private static List<CircleGenerator.CircleRecord> ReadCircleRecordsForWeb(string circlePath)
        {
            const int recordSize = 3 * sizeof(int);
            FileInfo fileInfo = new(circlePath);
            if (fileInfo.Length % recordSize != 0)
            {
                throw new InvalidOperationException($"{CircleGenerator.CircleFileName} file length is invalid.");
            }

            List<CircleGenerator.CircleRecord> circles = new((int)(fileInfo.Length / recordSize));
            using FileStream stream = File.OpenRead(circlePath);
            using BinaryReader reader = new(stream);
            while (stream.Position < stream.Length)
            {
                circles.Add(new CircleGenerator.CircleRecord(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()));
            }

            return circles;
        }

        private static void WriteWebResponse(
            HttpListenerContext context,
            string contentType,
            string content,
            int statusCode = 200)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = $"{contentType}; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private const string WebIndexHtml = """
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>tradeRobot Three.js View</title>
  <style>
    html, body {
      width: 100%;
      height: 100%;
      margin: 0;
      overflow: hidden;
      background: #090b0f;
      color: #e8edf2;
      font-family: "Segoe UI", Arial, sans-serif;
    }

    #toolbar {
      position: fixed;
      left: 12px;
      top: 12px;
      z-index: 10;
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px;
      background: rgba(10, 14, 18, 0.78);
      border: 1px solid rgba(255, 255, 255, 0.12);
    }

    input, button {
      height: 30px;
      border: 1px solid rgba(255, 255, 255, 0.18);
      background: #111820;
      color: #e8edf2;
      font-size: 14px;
      border-radius: 4px;
    }

    input {
      width: 64px;
      padding: 0 8px;
    }

    label {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      height: 30px;
      color: #cdd6df;
      font-size: 13px;
      white-space: nowrap;
    }

    input[type="checkbox"] {
      width: 16px;
      height: 16px;
      padding: 0;
    }

    button {
      padding: 0 12px;
      cursor: pointer;
    }

    #status {
      min-width: 260px;
      color: #9fb1c1;
      font-size: 13px;
      white-space: nowrap;
    }

    canvas {
      display: block;
      width: 100%;
      height: 100%;
    }
  </style>
</head>
<body>
    <div id="toolbar">
      <input id="startSelector" type="number" value="0" />
      <input id="targetSelector" type="number" value="1" />
    <button id="reloadButton">加载</button>
    <button id="traverseButton">遍历</button>
    <button id="adaptStrokeButton">适配线宽</button>
    <label><input id="showLabels" type="checkbox" checked /> 文字</label>
    <label><input id="showLinks" type="checkbox" checked /> 联通</label>
    <label><input id="showPurchases" type="checkbox" checked /> 采购</label>
    <span id="status"></span>
  </div>
  <canvas id="scene"></canvas>

  <script type="importmap">
    {
      "imports": {
        "three": "https://cdn.jsdelivr.net/npm/three@0.164.1/build/three.module.js",
        "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.164.1/examples/jsm/"
      }
    }
  </script>
  <script type="module">
    import * as THREE from 'three';
    import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

    const canvas = document.getElementById('scene');
    const statusElement = document.getElementById('status');
    const traverseButton = document.getElementById('traverseButton');
    const adaptStrokeButton = document.getElementById('adaptStrokeButton');
    const showLabelsInput = document.getElementById('showLabels');
    const showLinksInput = document.getElementById('showLinks');
    const showPurchasesInput = document.getElementById('showPurchases');
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setClearColor(0x090b0f, 1);

    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const scene = new THREE.Scene();
    const worldGroup = new THREE.Group();
    const circleGroup = new THREE.Group();
    const linkGroup = new THREE.Group();
    const purchaseGroup = new THREE.Group();
    const routeGroup = new THREE.Group();
    const labelGroup = new THREE.Group();
    scene.add(worldGroup);
    scene.add(circleGroup);
    scene.add(linkGroup);
    scene.add(purchaseGroup);
    scene.add(routeGroup);
    scene.add(labelGroup);
    const camera = new THREE.OrthographicCamera(-80, 80, 45, -45, 0.1, 10000);
    camera.position.set(0, 0, 500);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableRotate = false;
    controls.enableZoom = true;
    controls.enablePan = true;
    controls.enableDamping = true;
    controls.screenSpacePanning = true;
    controls.zoomSpeed = 1.2;
    controls.panSpeed = 1.0;
    controls.mouseButtons.LEFT = THREE.MOUSE.PAN;
    controls.mouseButtons.RIGHT = THREE.MOUSE.PAN;
    controls.touches.ONE = THREE.TOUCH.PAN;
    controls.touches.TWO = THREE.TOUCH.DOLLY_PAN;

    window.tradeRobotView = {
      getView() {
        return {
          cameraX: camera.position.x,
          cameraY: camera.position.y,
          cameraZoom: camera.zoom,
          targetX: controls.target.x,
          targetY: controls.target.y,
          worldChildren: worldGroup.children.length,
          circleChildren: circleGroup.children.length,
          linkChildren: linkGroup.children.length,
          purchaseChildren: purchaseGroup.children.length,
          routeChildren: routeGroup.children.length,
          labelChildren: labelGroup.children.length,
          clickableChildren: clickableObjects.length,
          flashingChildren: flashingCircleStrokes.size,
          labelsVisible: labelGroup.visible,
        };
      }
    };

    let transform = {
      centerX: 0,
      centerY: 0,
      scale: 0.0001,
    };
    const labelSprites = [];
    const scalableTubes = [];
    const scalableCircleStrokes = [];
    const clickableObjects = [];
    const circleStrokeByIndex = new Map();
    const flashingCircleStrokes = new Set();
    let lastStrokeZoom = 0;
    let pointerDownPosition = null;
    let labelPreferenceTouched = false;
    let linkPreferenceTouched = false;
    let purchasePreferenceTouched = false;
    let traverseRunning = false;
    let traverseTimer = 0;
    let traverseStartSelector = 0;
    let traverseTargetSelector = 1;
    let traverseSelectorCount = 0;
    let traverseStepIndex = 0;

    function resize() {
      const width = window.innerWidth;
      const height = window.innerHeight;
      renderer.setSize(width, height, false);
      const aspect = width / Math.max(height, 1);
      const viewHeight = 160;
      camera.left = -viewHeight * aspect * 0.5;
      camera.right = viewHeight * aspect * 0.5;
      camera.top = viewHeight * 0.5;
      camera.bottom = -viewHeight * 0.5;
      camera.updateProjectionMatrix();
    }

    function rawToVector(x, y, z = 0) {
      return new THREE.Vector3(
        (x - transform.centerX) * transform.scale,
        (y - transform.centerY) * transform.scale,
        z);
    }

    function clearScene() {
      clearGroup(worldGroup);
      clearGroup(circleGroup);
      clearGroup(linkGroup);
      clearGroup(purchaseGroup);
      clearGroup(routeGroup);
      clearGroup(labelGroup);
      labelSprites.length = 0;
      scalableTubes.length = 0;
      scalableCircleStrokes.length = 0;
      clickableObjects.length = 0;
      circleStrokeByIndex.clear();
      flashingCircleStrokes.clear();
    }

    function clearGroup(group) {
      while (group.children.length > 0) {
        const child = group.children.pop();
        child.traverse?.((item) => {
          item.geometry?.dispose?.();
          if (Array.isArray(item.material)) {
            item.material.forEach((material) => material.dispose?.());
          } else {
            item.material?.dispose?.();
          }
        });
      }
    }

    function updateLabelVisibility() {
      labelGroup.visible = showLabelsInput.checked;
    }

    function updateLayerVisibility() {
      updateLabelVisibility();
      linkGroup.visible = showLinksInput.checked;
      purchaseGroup.visible = showPurchasesInput.checked;
    }

    function updateLabelScreenSize() {
      const inverseZoom = 1 / Math.max(camera.zoom, 0.0001);
      for (const sprite of labelSprites) {
        sprite.scale.set(
          sprite.userData.baseScaleX * inverseZoom,
          sprite.userData.baseScaleY * inverseZoom,
          1);
      }
    }

    function updateTransform(data) {
      let minX = Infinity;
      let minY = Infinity;
      let maxX = -Infinity;
      let maxY = -Infinity;

      for (const circle of data.circles) {
        minX = Math.min(minX, circle.a - circle.radius);
        maxX = Math.max(maxX, circle.a + circle.radius);
        minY = Math.min(minY, circle.b - circle.radius);
        maxY = Math.max(maxY, circle.b + circle.radius);
      }

      transform.centerX = (minX + maxX) * 0.5;
      transform.centerY = (minY + maxY) * 0.5;
      const range = Math.max(maxX - minX, maxY - minY, 1);
      transform.scale = 130 / range;
    }

    function makeLine(points, color, opacity = 1, group = worldGroup) {
      const geometry = new THREE.BufferGeometry().setFromPoints(points);
      const material = new THREE.LineBasicMaterial({
        color,
        transparent: opacity < 1,
        opacity,
        depthWrite: false,
      });
      const line = new THREE.Line(geometry, material);
      group.add(line);
      return line;
    }

    function makeTube(points, color, radius, opacity = 1, options = {}) {
      if (points.length < 2) {
        return;
      }

      const storedPoints = points.map((point) => point.clone());
      const segments = Math.max(8, storedPoints.length * 6);
      const geometry = createTubeGeometry(storedPoints, radius, segments);
      const material = new THREE.MeshBasicMaterial({
        color,
        transparent: opacity < 1,
        opacity,
        depthWrite: false,
        depthTest: options.depthTest ?? true,
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.renderOrder = options.renderOrder ?? 0;
      mesh.userData.scalableTube = {
        points: storedPoints,
        baseRadius: radius,
        segments,
      };
      scalableTubes.push(mesh);
      const group = options.group ?? worldGroup;
      group.add(mesh);
      return mesh;
    }

    function createTubeGeometry(points, baseRadius, segments) {
      const curve = new THREE.CatmullRomCurve3(points);
      return new THREE.TubeGeometry(curve, segments, scaledStrokeSize(baseRadius), 8, false);
    }

    function makeCircleStroke(center, radius, color, opacity = 1) {
      const baseStrokeWidth = Math.max(0.22, radius * 0.006);
      const geometry = createCircleStrokeGeometry(radius, baseStrokeWidth);
      const material = new THREE.MeshBasicMaterial({
        color,
        transparent: opacity < 1,
        opacity,
        depthWrite: false,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.position.copy(center);
      mesh.userData.scalableCircleStroke = {
        radius,
        baseStrokeWidth,
      };
      mesh.userData.baseColor = color;
      mesh.userData.baseOpacity = opacity;
      scalableCircleStrokes.push(mesh);
      circleGroup.add(mesh);
      return mesh;
    }

    function createCircleStrokeGeometry(radius, baseStrokeWidth) {
      const strokeWidth = scaledStrokeSize(baseStrokeWidth);
      const innerRadius = Math.max(radius - strokeWidth * 0.5, 0.001);
      const outerRadius = radius + strokeWidth * 0.5;
      return new THREE.RingGeometry(innerRadius, outerRadius, 192);
    }

    function scaledStrokeSize(baseSize) {
      return baseSize / Math.max(camera.zoom, 0.0001);
    }

    function updateScalableStrokeWidths(force = false) {
      const zoom = Math.max(camera.zoom, 0.0001);
      if (!force && Math.abs(zoom - lastStrokeZoom) < 0.0001) {
        return;
      }

      lastStrokeZoom = zoom;

      for (const mesh of scalableTubes) {
        const data = mesh.userData.scalableTube;
        if (!data) {
          continue;
        }

        mesh.geometry.dispose();
        mesh.geometry = createTubeGeometry(data.points, data.baseRadius, data.segments);
      }

      for (const mesh of scalableCircleStrokes) {
        const data = mesh.userData.scalableCircleStroke;
        if (!data) {
          continue;
        }

        mesh.geometry.dispose();
        mesh.geometry = createCircleStrokeGeometry(data.radius, data.baseStrokeWidth);
      }
    }

    function pointOnCircle(circle, pointIndex) {
      const direction = circle.r > 0 ? 1 : -1;
      const angle = direction * Math.PI * 2 * pointIndex / circle.pathPointCount;
      return {
        x: circle.a + circle.radius * Math.cos(angle),
        y: circle.b + circle.radius * Math.sin(angle),
      };
    }

    function makeCircle(circle) {
        const center = rawToVector(circle.a, circle.b, -0.04);
        const radius = circle.radius * transform.scale;
        const isLong = circle.r > 0;
        const edgeColor = isLong ? 0x55e38a : 0xff6b6b;

        const circleStroke = makeCircleStroke(new THREE.Vector3(center.x, center.y, 0.01), radius, edgeColor, 0.72);
        circleStrokeByIndex.set(circle.index, circleStroke);
        makeLabel(`#${circle.index} a=${circle.a} b=${circle.b} r=${circle.r}`, rawToVector(circle.a, circle.b, 0.12), 4.6, circle.index);
      }

    function makeLabel(text, position, size, circleIndex) {
      const canvas = document.createElement('canvas');
      const context = canvas.getContext('2d');
      context.font = '24px Segoe UI, Arial';
      const width = Math.ceil(context.measureText(text).width + 16);
      canvas.width = width;
      canvas.height = 36;
      context.font = '24px Segoe UI, Arial';
      context.fillStyle = 'rgba(7, 10, 14, 0.72)';
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.fillStyle = '#e8edf2';
      context.fillText(text, 8, 26);
      const texture = new THREE.CanvasTexture(canvas);
      const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false });
      const sprite = new THREE.Sprite(material);
      sprite.center.set(1, 1);
      sprite.position.copy(position);
      sprite.userData.baseScaleX = size * canvas.width / canvas.height;
      sprite.userData.baseScaleY = size;
      sprite.userData.flashCircleIndex = circleIndex;
      sprite.scale.set(sprite.userData.baseScaleX, sprite.userData.baseScaleY, 1);
      labelSprites.push(sprite);
      clickableObjects.push(sprite);
      labelGroup.add(sprite);
      updateLabelScreenSize();
    }

    function makeObjectFlashCircleOnClick(object, circleIndex) {
      object.userData.flashCircleIndex = circleIndex;
      clickableObjects.push(object);
    }

    function findFlashCircleIndex(object) {
      let current = object;
      while (current) {
        if (Number.isInteger(current.userData?.flashCircleIndex)) {
          return current.userData.flashCircleIndex;
        }

        current = current.parent;
      }

      return null;
    }

    function isObjectVisibleToUser(object) {
      let current = object;
      while (current) {
        if (!current.visible) {
          return false;
        }

        current = current.parent;
      }

      return true;
    }

    function flashCircle(circleIndex) {
      const circleStroke = circleStrokeByIndex.get(circleIndex);
      if (!circleStroke) {
        return;
      }

      circleStroke.userData.flashUntil = performance.now() + 1800;
      circleStroke.renderOrder = 2000;
      flashingCircleStrokes.add(circleStroke);
    }

    function updateFlashingCircles() {
      const now = performance.now();
      for (const circleStroke of Array.from(flashingCircleStrokes)) {
        const flashUntil = circleStroke.userData.flashUntil ?? 0;
        const baseColor = circleStroke.userData.baseColor;
        const baseOpacity = circleStroke.userData.baseOpacity;

        if (now >= flashUntil) {
          circleStroke.material.color.setHex(baseColor);
          circleStroke.material.opacity = baseOpacity;
          circleStroke.renderOrder = 0;
          flashingCircleStrokes.delete(circleStroke);
          continue;
        }

        const phase = Math.floor(now / 140) % 2;
        circleStroke.material.color.setHex(phase === 0 ? 0xffffff : 0xfacc15);
        circleStroke.material.opacity = phase === 0 ? 1 : 0.35;
      }
    }

    function handleSceneClick(event) {
      const rect = renderer.domElement.getBoundingClientRect();
      pointer.x = ((event.clientX - rect.left) / Math.max(rect.width, 1)) * 2 - 1;
      pointer.y = -((event.clientY - rect.top) / Math.max(rect.height, 1)) * 2 + 1;
      raycaster.setFromCamera(pointer, camera);

      const hits = raycaster.intersectObjects(clickableObjects, false);
      for (const hit of hits) {
        if (!isObjectVisibleToUser(hit.object)) {
          continue;
        }

        const circleIndex = findFlashCircleIndex(hit.object);
        if (circleIndex === null) {
          continue;
        }

        flashCircle(circleIndex);
        return;
      }
    }

    function drawConnections(data) {
      for (const connection of data.ordinaryConnections) {
        makeTube([
          rawToVector(connection.leftX, connection.leftY, 0.06),
          rawToVector(connection.rightX, connection.rightY, 0.06),
        ], 0xfacc15, 0.16, 0.82, { group: linkGroup });
      }

      for (const connection of data.terminalConnections) {
        makeTube([
          rawToVector(connection.sourceX, connection.sourceY, 0.05),
          rawToVector(connection.targetX, connection.targetY, 0.05),
        ], 0xfb923c, 0.14, 0.75, { group: linkGroup });
      }

      for (const assignment of data.purchaseAssignments) {
        const purchaseLine = makeTube([
          rawToVector(assignment.sourceX, assignment.sourceY, 0.07),
          rawToVector(assignment.targetX, assignment.targetY, 0.07),
        ], 0xfb923c, 0.15, 0.74, { group: purchaseGroup });
        makeObjectFlashCircleOnClick(purchaseLine, assignment.circleIndex);
      }
    }

    function buildRouteArcPoints(data, source, target) {
      const circle = data.circles[source.circleIndex];
      const direction = circle.r > 0 ? 1 : -1;
      let sourceAngle = direction * Math.PI * 2 * source.pointIndex / circle.pathPointCount;
      let targetAngle = direction * Math.PI * 2 * target.pointIndex / circle.pathPointCount;

      if (direction > 0) {
        while (targetAngle < sourceAngle) {
          targetAngle += Math.PI * 2;
        }
      } else {
        while (targetAngle > sourceAngle) {
          targetAngle -= Math.PI * 2;
        }
      }

      const sweep = Math.abs(targetAngle - sourceAngle);
      const steps = Math.max(8, Math.ceil(96 * sweep / (Math.PI * 2)));
      const points = [];
      for (let index = 0; index <= steps; index++) {
        const angle = sourceAngle + (targetAngle - sourceAngle) * index / steps;
        points.push(rawToVector(
          circle.a + circle.radius * Math.cos(angle),
          circle.b + circle.radius * Math.sin(angle),
          0.16));
      }

      return points;
    }

    function drawRoute(data) {
      if (data.routeError) {
        statusElement.textContent = data.routeError;
      }

      const routePoints = data.route.points;
      if (routePoints.length < 2) {
        return;
      }

      const routeGeometryPoints = [];
      for (let index = 0; index < routePoints.length - 1; index++) {
        const source = routePoints[index];
        const target = routePoints[index + 1];
        let segmentPoints;

        if (source.circleIndex === target.circleIndex &&
            !(source.pointIndex === data.circles[source.circleIndex].pathPointCount - 1 && target.pointIndex === 0)) {
          segmentPoints = buildRouteArcPoints(data, source, target);
        } else {
          segmentPoints = [
            rawToVector(source.x, source.y, 0.16),
            rawToVector(target.x, target.y, 0.16),
          ];
        }

        if (routeGeometryPoints.length > 0) {
          segmentPoints.shift();
        }

        routeGeometryPoints.push(...segmentPoints);
      }

      makeTube(routeGeometryPoints, 0xf8fafc, 1.55, 1, { depthTest: false, renderOrder: 998, group: routeGroup });
      makeTube(routeGeometryPoints, 0xd946ef, 1.02, 1, { depthTest: false, renderOrder: 999, group: routeGroup });
      makeTube(routeGeometryPoints, 0xfacc15, 0.38, 1, { depthTest: false, renderOrder: 1000, group: routeGroup });
      statusElement.textContent = `circles=${data.circles.length} routePoints=${routePoints.length}`;
    }

    async function loadScene() {
      const start = document.getElementById('startSelector').value || '0';
      const target = document.getElementById('targetSelector').value || '1';
      statusElement.textContent = 'loading';
      const response = await fetch(`/api/scene?start=${encodeURIComponent(start)}&target=${encodeURIComponent(target)}`);
      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || response.statusText);
      }

      clearScene();
      updateTransform(data);
      resize();
      for (const circle of data.circles) {
        makeCircle(circle);
      }
      drawConnections(data);
      drawRoute(data);
      if (!labelPreferenceTouched && data.circles.length > 50) {
        showLabelsInput.checked = false;
      }
      if (!linkPreferenceTouched && data.circles.length > 50) {
        showLinksInput.checked = false;
      }
      if (!purchasePreferenceTouched && data.circles.length > 50) {
        showPurchasesInput.checked = false;
      }
      updateLayerVisibility();
      controls.target.set(0, 0, 0);
      controls.update();
      return data;
    }

    function stopTraverse() {
      traverseRunning = false;
      traverseButton.textContent = '遍历';
      if (traverseTimer) {
        window.clearTimeout(traverseTimer);
        traverseTimer = 0;
      }
    }

    function moveToNextTraversePair() {
      if (traverseSelectorCount <= 1) {
        stopTraverse();
        statusElement.textContent = '遍历需要至少两个交易点。';
        return false;
      }

      const maxPairCount = traverseSelectorCount * (traverseSelectorCount - 1);
      for (let attempt = 0; attempt < maxPairCount; attempt++) {
        traverseTargetSelector++;
        if (traverseTargetSelector >= traverseSelectorCount) {
          traverseTargetSelector = 0;
          traverseStartSelector = (traverseStartSelector + 1) % traverseSelectorCount;
        }

        if (traverseStartSelector !== traverseTargetSelector) {
          return true;
        }
      }

      stopTraverse();
      statusElement.textContent = '遍历没有可用的起点终点组合。';
      return false;
    }

    async function runTraverseStep() {
      if (!traverseRunning) {
        return;
      }

      document.getElementById('startSelector').value = String(traverseStartSelector);
      document.getElementById('targetSelector').value = String(traverseTargetSelector);

      try {
        const data = await loadScene();
        traverseSelectorCount = data.circles.length + 1;
        const totalPairCount = traverseSelectorCount * (traverseSelectorCount - 1);
        traverseStepIndex = Math.min(traverseStepIndex + 1, totalPairCount);
        statusElement.textContent =
          `遍历 ${traverseStepIndex}/${totalPairCount}: ${traverseStartSelector} -> ${traverseTargetSelector}, ` +
          `circles=${data.circles.length} routePoints=${data.route.points.length}`;

        if (traverseStepIndex >= totalPairCount) {
          statusElement.textContent += '，完成';
          stopTraverse();
          return;
        }

        if (!moveToNextTraversePair()) {
          return;
        }

        traverseTimer = window.setTimeout(runTraverseStep, 5000);
      } catch (error) {
        statusElement.textContent = error.message;
        traverseTimer = window.setTimeout(runTraverseStep, 5000);
      }
    }

    async function startTraverse() {
      stopTraverse();
      traverseButton.textContent = '停止遍历';
      traverseRunning = true;
      traverseTimer = 0;
      traverseStepIndex = 0;
      traverseStartSelector = Number.parseInt(document.getElementById('startSelector').value || '0', 10);
      traverseTargetSelector = Number.parseInt(document.getElementById('targetSelector').value || '1', 10);

      if (!Number.isInteger(traverseStartSelector) || traverseStartSelector < 0) {
        traverseStartSelector = 0;
      }

      if (!Number.isInteger(traverseTargetSelector) || traverseTargetSelector < 0) {
        traverseTargetSelector = 1;
      }

      if (traverseStartSelector === traverseTargetSelector) {
        traverseTargetSelector = traverseStartSelector === 0 ? 1 : 0;
      }

      runTraverseStep();
    }

    document.getElementById('reloadButton').addEventListener('click', () => {
      stopTraverse();
      loadScene().catch((error) => {
        statusElement.textContent = error.message;
      });
    });

    traverseButton.addEventListener('click', () => {
      if (traverseRunning) {
        stopTraverse();
        return;
      }

      startTraverse().catch((error) => {
        stopTraverse();
        statusElement.textContent = error.message;
      });
    });

    adaptStrokeButton.addEventListener('click', () => {
      updateScalableStrokeWidths(true);
    });

    renderer.domElement.addEventListener('pointerdown', (event) => {
      pointerDownPosition = {
        x: event.clientX,
        y: event.clientY,
      };
    });

    renderer.domElement.addEventListener('pointerup', (event) => {
      if (!pointerDownPosition) {
        return;
      }

      const dx = event.clientX - pointerDownPosition.x;
      const dy = event.clientY - pointerDownPosition.y;
      pointerDownPosition = null;

      if (dx * dx + dy * dy > 16) {
        return;
      }

      handleSceneClick(event);
    });

    showLabelsInput.addEventListener('change', () => {
      labelPreferenceTouched = true;
      updateLayerVisibility();
    });

    showLinksInput.addEventListener('change', () => {
      linkPreferenceTouched = true;
      updateLayerVisibility();
    });

    showPurchasesInput.addEventListener('change', () => {
      purchasePreferenceTouched = true;
      updateLayerVisibility();
    });

    window.addEventListener('resize', resize);
    resize();
    loadScene().catch((error) => {
      statusElement.textContent = error.message;
    });

    function animate() {
      controls.update();
      updateLabelScreenSize();
      updateFlashingCircles();
      renderer.render(scene, camera);
      requestAnimationFrame(animate);
    }

    animate();
  </script>
</body>
</html>
""";
    }
}
