using ConsoleMain;

namespace TestProject1
{
    /// <summary>
    /// CircleGenerator 的边界测试，主要保护圆生成规则里的半径、坐标范围、连通判定和二进制格式。
    /// </summary>
    public class CircleGeneratorTests
    {
        /// <summary>
        /// 备注：最大半径必须取坐标系横向、纵向最大范围里的较小值。
        /// </summary>
        [Test]
        public void MaxRadius_UsesSmallerCoordinateLimit()
        {
            Assert.That(CircleGenerator.MaxRadius, Is.EqualTo(Math.Min(CircleGenerator.MaxX, CircleGenerator.MaxY)));
        }

        /// <summary>
        /// 备注：生成文件名必须固定为 Circle.bin，防止误写成 Cirle.bin。
        /// </summary>
        [Test]
        public void CircleFileName_IsCircleBin()
        {
            Assert.That(CircleGenerator.CircleFileName, Is.EqualTo("Circle.bin"));
        }

        /// <summary>
        /// 备注：半径等于最小值时，只要圆周刚好贴住坐标边界，就仍然是合法圆。
        /// </summary>
        [Test]
        public void ValidateCircle_AllowsMinimumRadiusOnBoundary()
        {
            CircleGenerator.CircleRecord positiveXBoundary = new(
                CircleGenerator.MaxX - CircleGenerator.MinRadius,
                0,
                CircleGenerator.MinRadius);

            CircleGenerator.CircleRecord negativeYBoundary = new(
                0,
                -CircleGenerator.MaxY + CircleGenerator.MinRadius,
                -CircleGenerator.MinRadius);

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCircle(positiveXBoundary));
            Assert.DoesNotThrow(() => CircleGenerator.ValidateCircle(negativeYBoundary));
        }

        /// <summary>
        /// 备注：圆心在原点时，最大半径的做多圆和做空圆都应该合法。
        /// </summary>
        [Test]
        public void ValidateCircle_AllowsMaximumRadiusAtOriginForBothDirections()
        {
            CircleGenerator.CircleRecord longCircle = new(0, 0, CircleGenerator.MaxRadius);
            CircleGenerator.CircleRecord shortCircle = new(0, 0, -CircleGenerator.MaxRadius);

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCircle(longCircle));
            Assert.DoesNotThrow(() => CircleGenerator.ValidateCircle(shortCircle));
        }

        /// <summary>
        /// 备注：真实几何半径 R 小于最小半径时必须拒绝。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsRadiusBelowMinimum()
        {
            CircleGenerator.CircleRecord circle = new(0, 0, CircleGenerator.MinRadius - 1);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：带符号半径 r 不能为 0，因为真实几何半径 R 必须大于等于最小半径。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsZeroSignedRadius()
        {
            CircleGenerator.CircleRecord circle = new(0, 0, 0);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：int.MinValue 无法安全取绝对值，必须按非法半径拒绝。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsIntMinValueSignedRadius()
        {
            CircleGenerator.CircleRecord circle = new(0, 0, int.MinValue);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：X 方向超出边界 1 个单位时必须拒绝，避免圆周越出整体取值范围。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsPositiveXOutOfBoundsByOne()
        {
            CircleGenerator.CircleRecord circle = new(
                CircleGenerator.MaxX - CircleGenerator.MinRadius + 1,
                0,
                CircleGenerator.MinRadius);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：Y 方向负边界超出 1 个单位时必须拒绝，方向半径为负也不能放宽几何边界。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsNegativeYOutOfBoundsByOne()
        {
            CircleGenerator.CircleRecord circle = new(
                0,
                -CircleGenerator.MaxY + CircleGenerator.MinRadius - 1,
                -CircleGenerator.MinRadius);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：X 方向负边界超出 1 个单位时必须拒绝。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsNegativeXOutOfBoundsByOne()
        {
            CircleGenerator.CircleRecord circle = new(
                -CircleGenerator.MaxX + CircleGenerator.MinRadius - 1,
                0,
                CircleGenerator.MinRadius);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：Y 方向正边界超出 1 个单位时必须拒绝。
        /// </summary>
        [Test]
        public void ValidateCircle_RejectsPositiveYOutOfBoundsByOne()
        {
            CircleGenerator.CircleRecord circle = new(
                0,
                CircleGenerator.MaxY - CircleGenerator.MinRadius + 1,
                CircleGenerator.MinRadius);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCircle(circle));
        }

        /// <summary>
        /// 备注：两个圆外切时只有一个交点，但需求里外切也算连通。
        /// </summary>
        [Test]
        public void HasCircumferenceIntersectionOrTangency_ReturnsTrueForOuterTangency()
        {
            CircleGenerator.CircleRecord left = new(0, 0, 10);
            CircleGenerator.CircleRecord right = new(20, 0, -10);

            Assert.That(CircleGenerator.HasCircumferenceIntersectionOrTangency(left, right), Is.True);
        }

        /// <summary>
        /// 备注：一个圆在另一个圆内部并且内切时，圆周仍有一个交点，也算连通。
        /// </summary>
        [Test]
        public void HasCircumferenceIntersectionOrTangency_ReturnsTrueForInnerTangency()
        {
            CircleGenerator.CircleRecord outer = new(0, 0, 30);
            CircleGenerator.CircleRecord inner = new(20, 0, -10);

            Assert.That(CircleGenerator.HasCircumferenceIntersectionOrTangency(outer, inner), Is.True);
        }

        /// <summary>
        /// 备注：两个圆周有两个交点时，这是标准相交，必须算连通。
        /// </summary>
        [Test]
        public void HasCircumferenceIntersectionOrTangency_ReturnsTrueForTwoIntersections()
        {
            CircleGenerator.CircleRecord left = new(0, 0, 10);
            CircleGenerator.CircleRecord right = new(15, 0, -10);

            Assert.That(CircleGenerator.HasCircumferenceIntersectionOrTangency(left, right), Is.True);
        }

        /// <summary>
        /// 备注：两个圆距离大于半径和时完全分离，不允许算连通。
        /// </summary>
        [Test]
        public void HasCircumferenceIntersectionOrTangency_ReturnsFalseForSeparateCircles()
        {
            CircleGenerator.CircleRecord left = new(0, 0, 10);
            CircleGenerator.CircleRecord right = new(21, 0, -10);

            Assert.That(CircleGenerator.HasCircumferenceIntersectionOrTangency(left, right), Is.False);
        }

        /// <summary>
        /// 备注：一个圆完全包住另一个圆但圆周没有交点时，不算相交，也不算连通。
        /// </summary>
        [Test]
        public void HasCircumferenceIntersectionOrTangency_ReturnsFalseForContainedWithoutTouching()
        {
            CircleGenerator.CircleRecord outer = new(0, 0, 30);
            CircleGenerator.CircleRecord inner = new(5, 0, -10);

            Assert.That(CircleGenerator.HasCircumferenceIntersectionOrTangency(outer, inner), Is.False);
        }

        /// <summary>
        /// 备注：坐标差接近整体边界时，相交判断必须使用 long 避免 int 溢出。
        /// </summary>
        [Test]
        public void HasCircumferenceIntersectionOrTangency_HandlesLargeCoordinateDistance()
        {
            CircleGenerator.CircleRecord left = new(-CircleGenerator.MaxX + 10, 0, 10);
            CircleGenerator.CircleRecord right = new(CircleGenerator.MaxX - 10, 0, -10);

            Assert.That(CircleGenerator.HasCircumferenceIntersectionOrTangency(left, right), Is.False);
        }

        /// <summary>
        /// 备注：几何唯一性只看 a、b、R，正负方向不同也不能生成相同几何圆。
        /// </summary>
        [Test]
        public void CircleRecord_GeometryKey_IgnoresDirection()
        {
            CircleGenerator.CircleRecord longCircle = new(100, -200, 30);
            CircleGenerator.CircleRecord shortCircle = new(100, -200, -30);

            Assert.That(longCircle.GeometryKey, Is.EqualTo(shortCircle.GeometryKey));
        }

        /// <summary>
        /// 备注：生成数量为 2 时，只应得到两个固定种子圆，并且这两个种子圆本身满足连通规则。
        /// </summary>
        [Test]
        public void BuildConnectedCircles_StartsWithSeedCircles()
        {
            List<CircleGenerator.CircleRecord> circles = CircleGenerator.BuildConnectedCircles(2);

            Assert.That(circles, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(circles[0], Is.EqualTo(new CircleGenerator.CircleRecord(-400_000, 0, -499_999)));
                Assert.That(circles[1], Is.EqualTo(new CircleGenerator.CircleRecord(400_000, 0, 499_999)));
                Assert.That(
                    CircleGenerator.HasCircumferenceIntersectionOrTangency(circles[0], circles[1]),
                    Is.True);
            });
        }

        /// <summary>
        /// 备注：批量生成结果必须数量正确、全部合法、几何唯一，并且整体只有一个连通分量。
        /// </summary>
        [Test]
        public void BuildConnectedCircles_ProducesUniqueInBoundsConnectedSet()
        {
            const int count = 20;

            List<CircleGenerator.CircleRecord> circles = CircleGenerator.BuildConnectedCircles(count);

            Assert.That(circles, Has.Count.EqualTo(count));
            Assert.That(circles.Select(circle => circle.GeometryKey).ToHashSet(), Has.Count.EqualTo(count));

            foreach (CircleGenerator.CircleRecord circle in circles)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(circle.SignedRadius, Is.Not.EqualTo(0));
                    Assert.That(circle.Radius, Is.InRange(CircleGenerator.MinRadius, CircleGenerator.MaxRadius));
                    Assert.DoesNotThrow(() => CircleGenerator.ValidateCircle(circle));
                });
            }

            for (int index = 1; index < circles.Count; index++)
            {
                bool connectsToPreviousCircle = circles
                    .Take(index)
                    .Any(existing => CircleGenerator.HasCircumferenceIntersectionOrTangency(circles[index], existing));

                Assert.That(connectsToPreviousCircle, Is.True, $"Circle at index {index} must connect to an earlier circle.");
            }

            Assert.That(IsSingleConnectedComponent(circles), Is.True);
        }

        /// <summary>
        /// 备注：生成数量不能小于种子圆数量，否则无法满足初始化规则。
        /// </summary>
        [Test]
        public void GenerateCircle_RejectsCountBelowSeedCount()
        {
            string outputPath = CreateTemporaryCircleFilePath();

            Assert.Throws<ArgumentOutOfRangeException>(() => CircleGenerator.GenerateCircle(outputPath, 1));
            Assert.That(File.Exists(outputPath), Is.False);
        }

        /// <summary>
        /// 备注：输出目录不存在时，GenerateCircle 应自动创建目录并写入指定数量的圆记录。
        /// </summary>
        [Test]
        public void GenerateCircle_CreatesMissingDirectoryAndWritesRequestedRecordCount()
        {
            const int count = 8;
            string outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                Guid.NewGuid().ToString("N"));
            string outputPath = Path.Combine(outputDirectory, CircleGenerator.CircleFileName);

            try
            {
                CircleGenerator.GenerateCircle(outputPath, count);
                List<CircleGenerator.CircleRecord> circles = ReadCircleFile(outputPath);

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(outputDirectory), Is.True);
                    Assert.That(new FileInfo(outputPath).Length, Is.EqualTo(count * 3 * sizeof(int)));
                    Assert.That(circles, Has.Count.EqualTo(count));
                    Assert.That(circles[0], Is.EqualTo(new CircleGenerator.CircleRecord(-400_000, 0, -499_999)));
                    Assert.That(circles[1], Is.EqualTo(new CircleGenerator.CircleRecord(400_000, 0, 499_999)));
                    Assert.That(IsSingleConnectedComponent(circles), Is.True);
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory);
                }
            }
        }

        /// <summary>
        /// 备注：Circle.bin 已存在且数量不足时，应先读取已有圆，再在保持整体连通的前提下补齐。
        /// </summary>
        [Test]
        public void GenerateCircle_ExistingConnectedFileWithTooFewCircles_CompletesAndPreservesExistingRecords()
        {
            const int count = 6;
            string outputPath = CreateTemporaryCircleFilePath();

            CircleGenerator.CircleRecord first = new(-400_000, 0, -499_999);
            CircleGenerator.CircleRecord second = new(400_000, 0, 499_999);
            WriteCircleFile(outputPath, first, second);

            try
            {
                CircleGenerator.GenerateCircle(outputPath, count);
                List<CircleGenerator.CircleRecord> circles = ReadCircleFile(outputPath);

                Assert.Multiple(() =>
                {
                    Assert.That(circles, Has.Count.EqualTo(count));
                    Assert.That(circles[0], Is.EqualTo(first));
                    Assert.That(circles[1], Is.EqualTo(second));
                    Assert.That(circles.Select(circle => circle.GeometryKey).ToHashSet(), Has.Count.EqualTo(count));
                    Assert.That(IsSingleConnectedComponent(circles), Is.True);
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// 备注：Circle.bin 已存在但圆集合拆成多个不连通分量时，不能继续补圆，应直接报错。
        /// </summary>
        [Test]
        public void GenerateCircle_ExistingDisconnectedFile_Throws()
        {
            string outputPath = CreateTemporaryCircleFilePath();

            WriteCircleFile(
                outputPath,
                new CircleGenerator.CircleRecord(-900_000, 0, 10),
                new CircleGenerator.CircleRecord(900_000, 0, -10));

            try
            {
                Assert.Throws<InvalidOperationException>(() => CircleGenerator.GenerateCircle(outputPath, 4));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// 备注：Circle.bin 已存在但出现相同 (a, b, R) 时，违反几何唯一性，应直接报错。
        /// </summary>
        [Test]
        public void GenerateCircle_ExistingDuplicateGeometryFile_Throws()
        {
            string outputPath = CreateTemporaryCircleFilePath();

            WriteCircleFile(
                outputPath,
                new CircleGenerator.CircleRecord(0, 0, 10),
                new CircleGenerator.CircleRecord(0, 0, -10));

            try
            {
                Assert.Throws<InvalidOperationException>(() => CircleGenerator.GenerateCircle(outputPath, 4));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// 备注：Circle.bin 已存在但长度不是 12 字节记录的整数倍时，说明二进制文件损坏，应直接报错。
        /// </summary>
        [Test]
        public void GenerateCircle_ExistingCorruptedBinaryFile_Throws()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            File.WriteAllBytes(outputPath, new byte[] { 1, 2, 3, 4 });

            try
            {
                Assert.Throws<InvalidOperationException>(() => CircleGenerator.GenerateCircle(outputPath, 4));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// 备注：Circle.bin 每个圆固定写入三个 int，顺序必须是 a、b、r。
        /// </summary>
        [Test]
        public void GenerateCircle_WritesSeedRecordsAsThreeIntBinaryRecords()
        {
            string outputPath = CreateTemporaryCircleFilePath();

            try
            {
                CircleGenerator.GenerateCircle(outputPath, 2);

                Assert.That(new FileInfo(outputPath).Length, Is.EqualTo(2 * 3 * sizeof(int)));

                using FileStream stream = File.OpenRead(outputPath);
                using BinaryReader reader = new(stream);

                Assert.Multiple(() =>
                {
                    Assert.That(reader.ReadInt32(), Is.EqualTo(-400_000));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(-499_999));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(400_000));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(499_999));
                    Assert.That(stream.Position, Is.EqualTo(stream.Length));
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [Test]
        public void CalculatePathPointCount_MinRadiusUsesDensityMultiplier()
        {
            CircleGenerator.CircleRecord circle = new(0, 0, CircleGenerator.MinRadius);

            Assert.That(CircleGenerator.CalculatePathPointCount(circle), Is.EqualTo(18_601));
        }

        [Test]
        public void GenerateHarvestPoint_ReturnsCircleIndexAndPathPointIndex()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            WriteCircleFile(outputPath, new CircleGenerator.CircleRecord(0, 0, CircleGenerator.MinRadius));

            try
            {
                (int circleIndex, int pathPointIndex) = CircleGenerator.GenerateHarvestPoint(outputPath);

                Assert.Multiple(() =>
                {
                    Assert.That(circleIndex, Is.EqualTo(0));
                    Assert.That(pathPointIndex, Is.EqualTo(0));
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [Test]
        public void GenerateHarvestPoint_ReturnsNearestPathPointWithoutDistanceLimit()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            WriteCircleFile(outputPath, new CircleGenerator.CircleRecord(500, 0, CircleGenerator.MinRadius));

            try
            {
                (int circleIndex, int pathPointIndex) = CircleGenerator.GenerateHarvestPoint(outputPath);

                Assert.Multiple(() =>
                {
                    Assert.That(circleIndex, Is.EqualTo(0));
                    Assert.That(pathPointIndex, Is.Not.EqualTo(CircleGenerator.CalculatePathPointCount(
                        new CircleGenerator.CircleRecord(500, 0, CircleGenerator.MinRadius)) - 1));
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: same-position roles are merged by multiplying their prime role codes.
        /// </summary>
        [Test]
        public void PathPointRoleCodes_CombineByMultiplication()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CircleGenerator.HarvestPointCode, Is.EqualTo(2));
                Assert.That(CircleGenerator.FirstPathPointCode, Is.EqualTo(3));
                Assert.That(CircleGenerator.TerminalLinkPointCode, Is.EqualTo(5));
                Assert.That(CircleGenerator.OrdinaryLinkPointCode, Is.EqualTo(7));
                Assert.That(CircleGenerator.PurchasePointCode, Is.EqualTo(11));
                Assert.That(CircleGenerator.HarvestPointCode * CircleGenerator.FirstPathPointCode, Is.EqualTo(6));
                Assert.That(CircleGenerator.OrdinaryLinkPointCode * CircleGenerator.FirstPathPointCode, Is.EqualTo(21));
                Assert.That(CircleGenerator.PurchasePointCode * CircleGenerator.FirstPathPointCode, Is.EqualTo(33));
            });
        }

        /// <summary>
        /// Remark: the first path point is only a marker, so it can coexist with the global harvest point.
        /// </summary>
        [Test]
        public void GenerateDerivedPoints_AllowsHarvestPointToCarryFirstPathPointMarker()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            CircleGenerator.CircleRecord circle = new(0, 0, CircleGenerator.MinRadius);
            WriteCircleFile(outputPath, circle);

            try
            {
                IReadOnlyList<CircleGenerator.DerivedPoint> points = CircleGenerator.GenerateDerivedPoints(outputPath);
                int terminalPointIndex = CircleGenerator.CalculatePathPointCount(circle) - 1;

                Assert.Multiple(() =>
                {
                    Assert.That(points[0], Is.EqualTo(new CircleGenerator.DerivedPoint(0, 0, 6)));
                    Assert.That(points, Does.Contain(new CircleGenerator.DerivedPoint(0, terminalPointIndex, 5)));
                    Assert.That(points, Does.Contain(new CircleGenerator.DerivedPoint(0, 1, 11)));
                    Assert.That(points, Has.Count.EqualTo(3));
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: tangent circles have one intersection, so they must produce one ordinary connection pair.
        /// </summary>
        [Test]
        public void GenerateDerivedPoints_CreatesOrdinaryConnectionPointsForTangency()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            WriteCircleFile(
                outputPath,
                new CircleGenerator.CircleRecord(-10, 0, CircleGenerator.MinRadius),
                new CircleGenerator.CircleRecord(10, 0, -CircleGenerator.MinRadius));

            try
            {
                IReadOnlyList<CircleGenerator.DerivedPoint> points = CircleGenerator.GenerateDerivedPoints(outputPath);
                List<CircleGenerator.DerivedPoint> ordinaryPoints = points
                    .Where(point => point.PointType % CircleGenerator.OrdinaryLinkPointCode == 0)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(ordinaryPoints, Has.Count.EqualTo(2));
                    Assert.That(
                        ordinaryPoints.Select(point => point.CircleIndex).OrderBy(circleIndex => circleIndex),
                        Is.EqualTo(new[] { 0, 1 }));
                    Assert.That(
                        ordinaryPoints.All(point =>
                            point.PointType % CircleGenerator.HarvestPointCode != 0 &&
                            point.PointType % CircleGenerator.TerminalLinkPointCode != 0 &&
                            point.PointType % CircleGenerator.PurchasePointCode != 0),
                        Is.True);
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: CUDA receives a flat int array of (circleIndex, pointIndex, pointType) triples.
        /// </summary>
        [Test]
        public void BuildCudaPointArray_FlattensDerivedPointsAsTriples()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, 6),
                new(0, 18_600, 5),
                new(0, 1, 11),
            };

            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);

            Assert.That(cudaPoints, Is.EqualTo(new[] { 0, 0, 6, 0, 18_600, 5, 0, 1, 11 }));
        }

        /// <summary>
        /// Remark: empty point sets are valid and should produce an empty CUDA array.
        /// </summary>
        [Test]
        public void BuildCudaPointArray_AllowsEmptyPointSet()
        {
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(Array.Empty<CircleGenerator.DerivedPoint>());

            Assert.That(cudaPoints, Is.Empty);
        }

        /// <summary>
        /// Remark: connect uses sorted CUDA point order and terminal points jump back to the first path point.
        /// </summary>
        [Test]
        public void BuildConnectArray_MapsTerminalPointToFirstPathPoint()
        {
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(
                new[] { new CircleGenerator.CircleRecord(0, 0, CircleGenerator.MinRadius) });
            List<CircleGenerator.DerivedPoint> points = pointData.Points
                .OrderBy(point => point.CircleIndex)
                .ThenBy(point => point.PointIndex)
                .ThenBy(point => point.PointType)
                .ToList();

            int[] connect = CircleGenerator.BuildConnectArray(points, pointData.OrdinaryConnections);
            int firstIndex = points.FindIndex(point =>
                point.CircleIndex == 0 &&
                point.PointIndex == 0);
            int terminalIndex = points.FindIndex(point =>
                point.PointType % CircleGenerator.TerminalLinkPointCode == 0);

            Assert.Multiple(() =>
            {
                Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(terminalIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(connect[terminalIndex], Is.EqualTo(firstIndex));
                Assert.That(connect[firstIndex], Is.EqualTo(-1));
            });
        }

        /// <summary>
        /// Remark: ordinary connection points are bidirectional entries in connect.
        /// </summary>
        [Test]
        public void BuildConnectArray_MapsOrdinaryConnectionPointsBidirectionally()
        {
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(
                new[]
                {
                    new CircleGenerator.CircleRecord(-10, 0, CircleGenerator.MinRadius),
                    new CircleGenerator.CircleRecord(10, 0, -CircleGenerator.MinRadius),
                });
            List<CircleGenerator.DerivedPoint> points = pointData.Points
                .OrderBy(point => point.CircleIndex)
                .ThenBy(point => point.PointIndex)
                .ThenBy(point => point.PointType)
                .ToList();

            int[] connect = CircleGenerator.BuildConnectArray(points, pointData.OrdinaryConnections);
            CircleGenerator.ConnectionPair connection = pointData.OrdinaryConnections.Single();
            int leftIndex = points.FindIndex(point =>
                point.CircleIndex == connection.LeftCircleIndex &&
                point.PointIndex == connection.LeftPointIndex);
            int rightIndex = points.FindIndex(point =>
                point.CircleIndex == connection.RightCircleIndex &&
                point.PointIndex == connection.RightPointIndex);

            Assert.Multiple(() =>
            {
                Assert.That(leftIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(rightIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(connect[leftIndex], Is.EqualTo(rightIndex));
                Assert.That(connect[rightIndex], Is.EqualTo(leftIndex));
            });
        }

        /// <summary>
        /// Remark: connect values are indexes in the provided point list, matching cudaPoints triple order.
        /// </summary>
        [Test]
        public void BuildConnectArray_UsesProvidedPointOrderAsCudaPointIndexes()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 99, CircleGenerator.TerminalLinkPointCode),
                new(1, 8, CircleGenerator.OrdinaryLinkPointCode),
                new(0, 0, CircleGenerator.FirstPathPointCode),
                new(1, 9, CircleGenerator.OrdinaryLinkPointCode),
                new(2, 0, CircleGenerator.FirstPathPointCode),
            };
            CircleGenerator.ConnectionPair[] ordinaryConnections =
            {
                new(1, 8, 1, 9),
            };

            int[] connect = CircleGenerator.BuildConnectArray(points, ordinaryConnections);

            Assert.That(connect, Is.EqualTo(new[] { 2, 3, -1, 1, -1 }));
        }

        /// <summary>
        /// Remark: terminal points must point to an existing first path point on the same circle.
        /// </summary>
        [Test]
        public void BuildConnectArray_MissingTerminalTarget_Throws()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 99, CircleGenerator.TerminalLinkPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.BuildConnectArray(points, Array.Empty<CircleGenerator.ConnectionPair>()));
        }

        /// <summary>
        /// Remark: one CUDA point cannot be assigned two different connect targets.
        /// </summary>
        [Test]
        public void BuildConnectArray_ConflictingTargets_Throws()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 99, CircleGenerator.TerminalLinkPointCode * CircleGenerator.OrdinaryLinkPointCode),
                new(0, 0, CircleGenerator.FirstPathPointCode),
                new(1, 8, CircleGenerator.OrdinaryLinkPointCode),
            };
            CircleGenerator.ConnectionPair[] ordinaryConnections =
            {
                new(0, 99, 1, 8),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.BuildConnectArray(points, ordinaryConnections));
        }

        /// <summary>
        /// Remark: connect has one entry for every CUDA point triple, so its length is one third of cudaPoints.
        /// </summary>
        [Test]
        public void ValidateCudaPointConnectRatio_AllowsOneConnectEntryPerCudaPoint()
        {
            int[] cudaPoints =
            {
                0, 0, 6,
                0, 18_600, 5,
                0, 1, 11,
            };
            int[] connect =
            {
                -1,
                0,
                -1,
            };

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCudaPointConnectRatio(cudaPoints, connect));
        }

        /// <summary>
        /// Remark: mismatched cudaPoints/connect sizes would break CUDA point index lookup and must fail early.
        /// </summary>
        [Test]
        public void ValidateCudaPointConnectRatio_RejectsMismatchedRatio()
        {
            int[] cudaPoints =
            {
                0, 0, 6,
                0, 18_600, 5,
            };
            int[] connect =
            {
                -1,
            };

            Assert.Throws<ArgumentException>(() =>
                CircleGenerator.ValidateCudaPointConnectRatio(cudaPoints, connect));
        }

        /// <summary>
        /// Remark: cudaPoints itself must remain a sequence of triples before comparing it with connect.
        /// </summary>
        [Test]
        public void ValidateCudaPointConnectRatio_RejectsNonTripleCudaPointArray()
        {
            Assert.Throws<ArgumentException>(() =>
                CircleGenerator.ValidateCudaPointConnectRatio(new[] { 0, 0 }, Array.Empty<int>()));
        }

        /// <summary>
        /// Remark: the CUDA wrapper validates the triple layout before invoking native code.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_RejectsNonTripleLength()
        {
            Assert.Throws<ArgumentException>(() => CalpathCuda.AcceptPoints(new[] { 1, 2 }));
        }

        /// <summary>
        /// Remark: empty arrays do not need the native DLL and return a stable checksum.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_AllowsEmptyArrayWithoutNativeDll()
        {
            Assert.That(CalpathCuda.AcceptPoints(Array.Empty<int>()), Is.EqualTo(0));
        }

        /// <summary>
        /// Remark: public no-argument methods must use the current directory Circle.bin and Circle.dxf.
        /// </summary>
        [Test]
        public void PublicDefaultMethods_UseCurrentDirectoryFiles()
        {
            RunInTemporaryCurrentDirectory(directory =>
            {
                CircleGenerator.GenerateCircle();
                CircleGenerator.DrawToDxf();
                (int circleIndex, int pathPointIndex) = CircleGenerator.GenerateHarvestPoint();
                IReadOnlyList<CircleGenerator.DerivedPoint> points = CircleGenerator.GenerateDerivedPoints();

                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(Path.Combine(directory, CircleGenerator.CircleFileName)), Is.True);
                    Assert.That(File.Exists(Path.Combine(directory, CircleGenerator.DxfFileName)), Is.True);
                    Assert.That(circleIndex, Is.GreaterThanOrEqualTo(0));
                    Assert.That(pathPointIndex, Is.GreaterThanOrEqualTo(0));
                    Assert.That(points, Is.Not.Empty);
                });
            });
        }

        /// <summary>
        /// Remark: the list overload should calculate and merge derived point roles without using files.
        /// </summary>
        [Test]
        public void GenerateDerivedPoints_ListOverloadMergesSamePositionRoles()
        {
            IReadOnlyList<CircleGenerator.DerivedPoint> points = CircleGenerator.GenerateDerivedPoints(
                new[] { new CircleGenerator.CircleRecord(0, 0, CircleGenerator.MinRadius) });

            Assert.Multiple(() =>
            {
                Assert.That(points, Has.Count.EqualTo(3));
                Assert.That(points[0], Is.EqualTo(new CircleGenerator.DerivedPoint(0, 0, 6)));
                Assert.That(
                    points.Select(point => (point.CircleIndex, point.PointIndex)).Distinct().Count(),
                    Is.EqualTo(points.Count));
            });
        }

        /// <summary>
        /// Remark: missing input files should fail before any harvest or derived point calculation.
        /// </summary>
        [Test]
        public void GenerateHarvestPointAndDerivedPoints_MissingFile_Throw()
        {
            string inputPath = CreateTemporaryCircleFilePath();

            Assert.Multiple(() =>
            {
                Assert.Throws<FileNotFoundException>(() => CircleGenerator.GenerateHarvestPoint(inputPath));
                Assert.Throws<FileNotFoundException>(() => CircleGenerator.GenerateDerivedPoints(inputPath));
            });
        }

        /// <summary>
        /// Remark: DXF output should include long/short layers plus readable circle index and parameters.
        /// </summary>
        [Test]
        public void DrawToDxf_WritesCircleLayersAndLabels()
        {
            string inputPath = CreateTemporaryCircleFilePath();
            string outputPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{Guid.NewGuid():N}-{CircleGenerator.DxfFileName}");
            WriteCircleFile(
                inputPath,
                new CircleGenerator.CircleRecord(-400_000, 0, -499_999),
                new CircleGenerator.CircleRecord(400_000, 0, 499_999));

            try
            {
                CircleGenerator.DrawToDxf(inputPath, outputPath);
                string dxf = File.ReadAllText(outputPath);

                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(outputPath), Is.True);
                    Assert.That(dxf, Does.Contain("ShortCircle"));
                    Assert.That(dxf, Does.Contain("LongCircle"));
                    Assert.That(dxf, Does.Contain("#0 a=-400000 b=0 r=-499999"));
                    Assert.That(dxf, Does.Contain("#1 a=400000 b=0 r=499999"));
                });
            }
            finally
            {
                if (File.Exists(inputPath))
                {
                    File.Delete(inputPath);
                }

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: missing input should fail before writing a DXF file.
        /// </summary>
        [Test]
        public void DrawToDxf_MissingInputFile_Throws()
        {
            string inputPath = CreateTemporaryCircleFilePath();
            string outputPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{Guid.NewGuid():N}-{CircleGenerator.DxfFileName}");

            Assert.Throws<FileNotFoundException>(() => CircleGenerator.DrawToDxf(inputPath, outputPath));
            Assert.That(File.Exists(outputPath), Is.False);
        }

        /// <summary>
        /// Remark: point index 0 is the max-x pole; later points follow the sign of r.
        /// </summary>
        [Test]
        public void CalculatePathPointCoordinate_UsesSignedDirection()
        {
            CircleGenerator.CircleRecord longCircle = new(100, -50, 10);
            CircleGenerator.CircleRecord shortCircle = new(100, -50, -10);

            (double longStartX, double longStartY) = CircleGenerator.CalculatePathPointCoordinate(longCircle, 0, 4);
            (double longQuarterX, double longQuarterY) = CircleGenerator.CalculatePathPointCoordinate(longCircle, 1, 4);
            (double shortQuarterX, double shortQuarterY) = CircleGenerator.CalculatePathPointCoordinate(shortCircle, 1, 4);

            Assert.Multiple(() =>
            {
                Assert.That(longStartX, Is.EqualTo(110).Within(1e-9));
                Assert.That(longStartY, Is.EqualTo(-50).Within(1e-9));
                Assert.That(longQuarterX, Is.EqualTo(100).Within(1e-9));
                Assert.That(longQuarterY, Is.EqualTo(-40).Within(1e-9));
                Assert.That(shortQuarterX, Is.EqualTo(100).Within(1e-9));
                Assert.That(shortQuarterY, Is.EqualTo(-60).Within(1e-9));
            });
        }

        /// <summary>
        /// Remark: path point coordinate lookup must reject indexes outside 0..n-1.
        /// </summary>
        [Test]
        public void CalculatePathPointCoordinate_RejectsOutOfRangeIndex()
        {
            CircleGenerator.CircleRecord circle = new(0, 0, CircleGenerator.MinRadius);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CircleGenerator.CalculatePathPointCoordinate(circle, -1, 4));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CircleGenerator.CalculatePathPointCoordinate(circle, 4, 4));
            });
        }

        /// <summary>
        /// Remark: existing files with exactly the requested count should be validated and preserved.
        /// </summary>
        [Test]
        public void GenerateCircle_ExistingFileWithRequestedCount_PreservesRecords()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            CircleGenerator.CircleRecord first = new(-400_000, 0, -499_999);
            CircleGenerator.CircleRecord second = new(400_000, 0, 499_999);
            WriteCircleFile(outputPath, first, second);

            try
            {
                CircleGenerator.GenerateCircle(outputPath, 2);
                List<CircleGenerator.CircleRecord> circles = ReadCircleFile(outputPath);

                Assert.That(circles, Is.EqualTo(new[] { first, second }));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: an empty existing Circle.bin should be regenerated from the seed circles.
        /// </summary>
        [Test]
        public void GenerateCircle_EmptyExistingFile_RegeneratesRequestedCount()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            File.WriteAllBytes(outputPath, Array.Empty<byte>());

            try
            {
                CircleGenerator.GenerateCircle(outputPath, 2);
                List<CircleGenerator.CircleRecord> circles = ReadCircleFile(outputPath);

                Assert.Multiple(() =>
                {
                    Assert.That(circles, Has.Count.EqualTo(2));
                    Assert.That(circles[0], Is.EqualTo(new CircleGenerator.CircleRecord(-400_000, 0, -499_999)));
                    Assert.That(circles[1], Is.EqualTo(new CircleGenerator.CircleRecord(400_000, 0, 499_999)));
                });
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: requesting fewer records than an existing valid file should not truncate data.
        /// </summary>
        [Test]
        public void GenerateCircle_ExistingFileWithTooManyCircles_Throws()
        {
            string outputPath = CreateTemporaryCircleFilePath();
            WriteCircleFile(
                outputPath,
                new CircleGenerator.CircleRecord(-400_000, 0, -499_999),
                new CircleGenerator.CircleRecord(400_000, 0, 499_999),
                new CircleGenerator.CircleRecord(0, 0, 500_000));

            try
            {
                Assert.Throws<InvalidOperationException>(() => CircleGenerator.GenerateCircle(outputPath, 2));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Remark: Radius uses the absolute signed radius; ToString keeps the CUDA/debug tuple shape.
        /// </summary>
        [Test]
        public void Records_ExposeStableRadiusAndDerivedPointText()
        {
            CircleGenerator.CircleRecord shortCircle = new(1, 2, -30);
            CircleGenerator.DerivedPoint point = new(4, 5, 6);

            Assert.Multiple(() =>
            {
                Assert.That(shortCircle.Radius, Is.EqualTo(30));
                Assert.That(point.ToString(), Is.EqualTo("(4, 5, 6)"));
            });
        }

        private static string CreateTemporaryCircleFilePath()
        {
            return Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{Guid.NewGuid():N}-{CircleGenerator.CircleFileName}");
        }

        private static void RunInTemporaryCurrentDirectory(Action<string> action)
        {
            string originalDirectory = Environment.CurrentDirectory;
            string temporaryDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                Environment.CurrentDirectory = temporaryDirectory;
                action(temporaryDirectory);
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;

                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
        }

        private static List<CircleGenerator.CircleRecord> ReadCircleFile(string outputPath)
        {
            List<CircleGenerator.CircleRecord> circles = new();

            using FileStream stream = File.OpenRead(outputPath);
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

        private static void WriteCircleFile(string outputPath, params CircleGenerator.CircleRecord[] circles)
        {
            using FileStream stream = File.Create(outputPath);
            using BinaryWriter writer = new(stream);

            foreach (CircleGenerator.CircleRecord circle in circles)
            {
                writer.Write(circle.A);
                writer.Write(circle.B);
                writer.Write(circle.SignedRadius);
            }
        }

        private static bool IsSingleConnectedComponent(IReadOnlyList<CircleGenerator.CircleRecord> circles)
        {
            if (circles.Count == 0)
            {
                return true;
            }

            HashSet<int> visited = new() { 0 };
            Queue<int> pending = new();
            pending.Enqueue(0);

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();

                for (int next = 0; next < circles.Count; next++)
                {
                    if (visited.Contains(next))
                    {
                        continue;
                    }

                    if (!CircleGenerator.HasCircumferenceIntersectionOrTangency(circles[current], circles[next]))
                    {
                        continue;
                    }

                    visited.Add(next);
                    pending.Enqueue(next);
                }
            }

            return visited.Count == circles.Count;
        }
    }
}
