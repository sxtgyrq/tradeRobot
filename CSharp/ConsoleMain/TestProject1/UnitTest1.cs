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
        /// Remark: purchase points are selected from all path points, not only from the circle being assigned.
        /// </summary>
        [Test]
        public void GenerateDerivedPoints_PurchasePointCanComeFromAnyCircle()
        {
            CircleGenerator.CircleRecord targetCircle = new(0, 0, CircleGenerator.MinRadius);
            CircleGenerator.CircleRecord neighborCircle = new(5, 0, -CircleGenerator.MinRadius);
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(
                new[] { targetCircle, neighborCircle });

            List<CircleGenerator.DerivedPoint> purchasePoints = pointData.Points
                .Where(point => point.PointType % CircleGenerator.PurchasePointCode == 0)
                .ToList();
            var nearestPurchasePointToTargetCenter = purchasePoints
                .Select(point =>
                {
                    CircleGenerator.CircleRecord purchaseCircle = point.CircleIndex == 0
                        ? targetCircle
                        : neighborCircle;
                    int purchasePathPointCount = CircleGenerator.CalculatePathPointCount(purchaseCircle);
                    (double x, double y) = CircleGenerator.CalculatePathPointCoordinate(
                        purchaseCircle,
                        point.PointIndex,
                        purchasePathPointCount);

                    return new
                    {
                        Point = point,
                        DistanceSquaredToTargetCenter = x * x + y * y,
                    };
                })
                .OrderBy(item => item.DistanceSquaredToTargetCenter)
                .ThenBy(item => item.Point.CircleIndex)
                .ThenBy(item => item.Point.PointIndex)
                .First();

            Assert.Multiple(() =>
            {
                Assert.That(purchasePoints, Has.Count.EqualTo(2));
                Assert.That(nearestPurchasePointToTargetCenter.Point.CircleIndex, Is.EqualTo(1));
                Assert.That(nearestPurchasePointToTargetCenter.Point.PointType % CircleGenerator.HarvestPointCode, Is.Not.EqualTo(0));
                Assert.That(nearestPurchasePointToTargetCenter.Point.PointType % CircleGenerator.TerminalLinkPointCode, Is.Not.EqualTo(0));
                Assert.That(nearestPurchasePointToTargetCenter.Point.PointType % CircleGenerator.OrdinaryLinkPointCode, Is.Not.EqualTo(0));
                Assert.That(
                    nearestPurchasePointToTargetCenter.DistanceSquaredToTargetCenter,
                    Is.LessThan(targetCircle.Radius * targetCircle.Radius));
            });
        }

        /// <summary>
        /// Remark: purchase point assignment must match the requirement-level brute-force rule.
        /// </summary>
        [Test]
        public void GenerateDerivedPoints_PurchasePointsMatchBruteForceGlobalNearestAvailablePoints()
        {
            CircleGenerator.CircleRecord[] circles =
            {
                new(0, 0, CircleGenerator.MinRadius),
                new(0, 0, CircleGenerator.MinRadius * 2),
                new(15, 0, -CircleGenerator.MinRadius),
            };
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(circles);
            int[] pathPointCounts = circles
                .Select(CircleGenerator.CalculatePathPointCount)
                .ToArray();
            HashSet<(int CircleIndex, int PointIndex)> unavailablePoints =
                BuildUnavailableOccupyingPointsBeforePurchase(pointData.Points);
            List<(int CircleIndex, int PointIndex)> expectedPurchasePoints =
                CalculateExpectedPurchasePointsByBruteForce(circles, pathPointCounts, unavailablePoints);
            List<(int CircleIndex, int PointIndex)> actualPurchasePoints = pointData.Points
                .Where(point => point.PointType % CircleGenerator.PurchasePointCode == 0)
                .Select(point => (point.CircleIndex, point.PointIndex))
                .OrderBy(point => point.CircleIndex)
                .ThenBy(point => point.PointIndex)
                .ToList();
            List<(int CircleIndex, int PointIndex)> sortedExpectedPurchasePoints = expectedPurchasePoints
                .OrderBy(point => point.CircleIndex)
                .ThenBy(point => point.PointIndex)
                .ToList();
            List<(int CircleIndex, int PurchaseCircleIndex, int PurchasePointIndex)> actualPurchaseAssignments = pointData.PurchaseAssignments
                .Select(assignment => (assignment.CircleIndex, assignment.PurchaseCircleIndex, assignment.PurchasePointIndex))
                .ToList();
            List<(int CircleIndex, int PurchaseCircleIndex, int PurchasePointIndex)> expectedPurchaseAssignments = expectedPurchasePoints
                .Select((point, circleIndex) => (circleIndex, point.CircleIndex, point.PointIndex))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(actualPurchasePoints, Has.Count.EqualTo(circles.Length));
                Assert.That(actualPurchasePoints.Distinct().Count(), Is.EqualTo(circles.Length));
                Assert.That(actualPurchasePoints, Is.EqualTo(sortedExpectedPurchasePoints));
                Assert.That(pointData.PurchaseAssignments, Has.Count.EqualTo(circles.Length));
                Assert.That(actualPurchaseAssignments, Is.EqualTo(expectedPurchaseAssignments));
                Assert.That(
                    pointData.Points
                        .Where(point => point.PointType % CircleGenerator.PurchasePointCode == 0)
                        .All(point =>
                            point.PointType % CircleGenerator.HarvestPointCode != 0 &&
                            point.PointType % CircleGenerator.TerminalLinkPointCode != 0 &&
                            point.PointType % CircleGenerator.OrdinaryLinkPointCode != 0),
                    Is.True);
            });
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
        /// 备注：进入 CUDA 前，合并后的点集必须已经按 circleIndex、pointIndex、pointType 升序排列。
        /// </summary>
        [Test]
        public void ValidateCudaPointOrderAndUniqueness_AllowsAscendingUniquePoints()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, 6),
                new(0, 1, 11),
                new(1, 0, 3),
            };

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCudaPointOrderAndUniqueness(points));
        }

        /// <summary>
        /// 备注：合并后的点集中不能存在相同的 (circleIndex, pointIndex)，否则 CUDA 点序号会产生歧义。
        /// </summary>
        [Test]
        public void ValidateCudaPointOrderAndUniqueness_RejectsDuplicateCircleAndPointIndex()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, 2),
                new(0, 0, 3),
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaPointOrderAndUniqueness(points));
        }

        /// <summary>
        /// 备注：合并后的点集如果不是升序排列，应在构建 cudaPoints 和 connect 前直接报错。
        /// </summary>
        [Test]
        public void ValidateCudaPointOrderAndUniqueness_RejectsOutOfOrderPoints()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 1, 11),
                new(0, 0, 6),
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaPointOrderAndUniqueness(points));
        }

        /// <summary>
        /// 备注：CUDA 点数量应满足 cudaPointCount = H + F + T + O + P - overlapCount。
        /// </summary>
        [Test]
        public void ValidateCudaPointRoleCount_AllowsRoleCountRelationWithOverlaps()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, 6),
                new(0, 10, CircleGenerator.TerminalLinkPointCode),
                new(1, 0, 33),
                new(1, 20, CircleGenerator.OrdinaryLinkPointCode),
            };
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCudaPointRoleCount(points, cudaPoints));
        }

        /// <summary>
        /// 备注：cudaPoints 中的三元组数量必须等于合并后的 CUDA 点数量。
        /// </summary>
        [Test]
        public void ValidateCudaPointRoleCount_RejectsMismatchedCudaPointCount()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.HarvestPointCode),
            };
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 0, CircleGenerator.FirstPathPointCode,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaPointRoleCount(points, cudaPoints));
        }

        /// <summary>
        /// 备注：如果某个合并点没有任何已知角色，角色数量公式会和 CUDA 点数量不一致，应直接报错。
        /// </summary>
        [Test]
        public void ValidateCudaPointRoleCount_RejectsPointWithoutKnownRole()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.NormalPathPointCode),
            };
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaPointRoleCount(points, cudaPoints));
        }

        /// <summary>
        /// 备注：进入 CUDA 前，合并点必须满足 pointType、占用角色、关键数量和 circleIndex/pointIndex 范围规则。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_AllowsValidMergedPointSet()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.FirstPathPointCode),
                new(0, 1, CircleGenerator.HarvestPointCode),
                new(0, 2, CircleGenerator.OrdinaryLinkPointCode),
                new(0, 3, CircleGenerator.PurchasePointCode),
                new(0, 4, CircleGenerator.TerminalLinkPointCode),
                new(1, 0, CircleGenerator.FirstPathPointCode),
                new(1, 3, CircleGenerator.OrdinaryLinkPointCode),
                new(1, 4, CircleGenerator.PurchasePointCode),
                new(1, 5, CircleGenerator.TerminalLinkPointCode),
            };
            int[] pathPointCounts =
            {
                5,
                6,
            };

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCudaPointBusinessRules(points, pathPointCounts));
        }

        /// <summary>
        /// 备注：pointType 只能由 2、3、5、7、11 这些角色编码因子组成。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsUnknownPointTypeFactor()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, 13),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：同一个角色编码质因子不能重复出现，例如 4 表示收割点角色重复，属于非法 pointType。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsRepeatedRoleFactor()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.HarvestPointCode * CircleGenerator.HarvestPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：收割点、末尾联通点、普通联通点、采购点是占用型角色，同一点不能同时占两个。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsMultipleOccupyingRoles()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 1, CircleGenerator.HarvestPointCode * CircleGenerator.OrdinaryLinkPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：circleIndex 必须落在圆数量范围内，pointIndex 必须落在该圆路径点数量范围内。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsPointOutsideCircleOrPathRange()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(1, 0, CircleGenerator.FirstPathPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：第一路径点必须是 pointIndex 0。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsFirstPathPointNotAtZero()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 1, CircleGenerator.FirstPathPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：末尾联通点必须是当前圆的 n - 1。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsTerminalLinkPointNotAtLastIndex()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 3, CircleGenerator.TerminalLinkPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：收割点、普通联通点、采购点不能占用 n - 1 末尾联通点位置。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsOccupyingRoleAtTerminalIndex()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 4, CircleGenerator.HarvestPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：收割点必须全场唯一，第一路径点、末尾联通点、采购点必须分别等于圆数量。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsInvalidRequiredRoleCounts()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.FirstPathPointCode),
                new(0, 4, CircleGenerator.TerminalLinkPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
        }

        /// <summary>
        /// 备注：普通联通点来自双向圆间联通关系，数量必须成对。
        /// </summary>
        [Test]
        public void ValidateCudaPointBusinessRules_RejectsOddOrdinaryLinkPointCount()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.FirstPathPointCode),
                new(0, 1, CircleGenerator.HarvestPointCode),
                new(0, 2, CircleGenerator.OrdinaryLinkPointCode),
                new(0, 3, CircleGenerator.PurchasePointCode),
                new(0, 4, CircleGenerator.TerminalLinkPointCode),
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointBusinessRules(points, new[] { 5 }));
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
        /// 备注：connect 可以表达末尾联通点的单向跳转，以及普通联通点的双向跳转。
        /// </summary>
        [Test]
        public void ValidateCudaConnectArray_AllowsTerminalAndBidirectionalOrdinaryLinks()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.FirstPathPointCode),
                new(0, 99, CircleGenerator.TerminalLinkPointCode),
                new(1, 8, CircleGenerator.OrdinaryLinkPointCode),
                new(2, 9, CircleGenerator.OrdinaryLinkPointCode),
            };
            int[] connect =
            {
                -1,
                0,
                3,
                2,
            };

            Assert.DoesNotThrow(() => CircleGenerator.ValidateCudaConnectArray(points, connect));
        }

        /// <summary>
        /// 备注：connect 的目标序号必须落在 CUDA 点序号范围内。
        /// </summary>
        [Test]
        public void ValidateCudaConnectArray_RejectsOutOfRangeTarget()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 99, CircleGenerator.TerminalLinkPointCode),
                new(0, 0, CircleGenerator.FirstPathPointCode),
            };
            int[] connect =
            {
                2,
                -1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaConnectArray(points, connect));
        }

        /// <summary>
        /// 备注：只有末尾联通点或普通联通点允许拥有 connect 目标。
        /// </summary>
        [Test]
        public void ValidateCudaConnectArray_RejectsConnectTargetOnNonLinkPoint()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.HarvestPointCode),
                new(0, 1, CircleGenerator.PurchasePointCode),
            };
            int[] connect =
            {
                1,
                -1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaConnectArray(points, connect));
        }

        /// <summary>
        /// 备注：末尾联通点只能单向连接到同圆第一路径点。
        /// </summary>
        [Test]
        public void ValidateCudaConnectArray_RejectsTerminalTargetThatIsNotSameCircleFirstPoint()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 99, CircleGenerator.TerminalLinkPointCode),
                new(1, 0, CircleGenerator.FirstPathPointCode),
            };
            int[] connect =
            {
                1,
                -1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaConnectArray(points, connect));
        }

        /// <summary>
        /// 备注：普通联通点必须形成双向 connect 关系。
        /// </summary>
        [Test]
        public void ValidateCudaConnectArray_RejectsOrdinaryLinkThatIsNotBidirectional()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 8, CircleGenerator.OrdinaryLinkPointCode),
                new(1, 9, CircleGenerator.OrdinaryLinkPointCode),
            };
            int[] connect =
            {
                1,
                -1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaConnectArray(points, connect));
        }

        /// <summary>
        /// 备注：普通联通点表达圆间联通关系，不能连接到同一个圆上的另一个普通联通点。
        /// </summary>
        [Test]
        public void ValidateCudaConnectArray_RejectsOrdinaryLinkOnSameCircle()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 8, CircleGenerator.OrdinaryLinkPointCode),
                new(0, 9, CircleGenerator.OrdinaryLinkPointCode),
            };
            int[] connect =
            {
                1,
                0,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateCudaConnectArray(points, connect));
        }

        /// <summary>
        /// 备注：tradingPoints 数量必须等于收割点数量加采购点数量，且按 CUDA 点序号升序唯一。
        /// </summary>
        [Test]
        public void ValidateTradingPoints_AllowsExpectedSortedUniqueTradingPoints()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                0, 1, CircleGenerator.FirstPathPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] tradingPoints =
            {
                0,
                2,
            };

            Assert.DoesNotThrow(() => CircleGenerator.ValidateTradingPoints(cudaPoints, tradingPoints, 1, 1));
        }

        /// <summary>
        /// 备注：tradingPoints.Count 必须等于 harvestPointCount + purchasePointCount。
        /// </summary>
        [Test]
        public void ValidateTradingPoints_RejectsCountMismatch()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] tradingPoints =
            {
                0,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateTradingPoints(cudaPoints, tradingPoints, 1, 1));
        }

        /// <summary>
        /// 备注：tradingPoints 必须升序且不能重复。
        /// </summary>
        [Test]
        public void ValidateTradingPoints_RejectsUnsortedOrDuplicateIndexes()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] tradingPoints =
            {
                1,
                0,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateTradingPoints(cudaPoints, tradingPoints, 1, 1));
        }

        /// <summary>
        /// 备注：tradingPoints 中的每个点都必须是真正的交易点。
        /// </summary>
        [Test]
        public void ValidateTradingPoints_RejectsNonTradingPointIndex()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.FirstPathPointCode,
            };
            int[] tradingPoints =
            {
                0,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateTradingPoints(cudaPoints, tradingPoints, 0, 1));
        }

        /// <summary>
        /// 备注：tradingPoints 必须包含 cudaPoints 中扫描出来的全部交易点，不能漏掉真实交易点。
        /// </summary>
        [Test]
        public void ValidateTradingPoints_RejectsMissingActualTradingPoint()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 1, CircleGenerator.PurchasePointCode,
                2, 2, CircleGenerator.PurchasePointCode,
            };
            int[] tradingPoints =
            {
                0,
                1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateTradingPoints(cudaPoints, tradingPoints, 1, 1));
        }

        /// <summary>
        /// 备注：每个 lastFP unit 必须只有一个交易起点，并且该起点前驱记录为自身。
        /// </summary>
        [Test]
        public void ValidateLastFPInitialization_AllowsOneTradingStartPerUnit()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                0, 1, CircleGenerator.FirstPathPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] lastFP =
            {
                0, -1, -1,
                -1, -1, 2,
            };

            Assert.DoesNotThrow(() => CircleGenerator.ValidateLastFPInitialization(cudaPoints, lastFP));
        }

        /// <summary>
        /// 备注：同一个 lastFP unit 不能初始化多个起点。
        /// </summary>
        [Test]
        public void ValidateLastFPInitialization_RejectsMoreThanOneStartInUnit()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] lastFP =
            {
                0, 1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateLastFPInitialization(cudaPoints, lastFP));
        }

        /// <summary>
        /// 备注：lastFP 起点必须记录自己，表示回溯终止。
        /// </summary>
        [Test]
        public void ValidateLastFPInitialization_RejectsStartThatDoesNotPointToItself()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] lastFP =
            {
                1, -1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateLastFPInitialization(cudaPoints, lastFP));
        }

        /// <summary>
        /// 备注：lastFP 初始化的起点必须是交易点。
        /// </summary>
        [Test]
        public void ValidateLastFPInitialization_RejectsNonTradingStart()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.FirstPathPointCode,
            };
            int[] lastFP =
            {
                0,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateLastFPInitialization(cudaPoints, lastFP));
        }

        /// <summary>
        /// 备注：每个 lastFP unit 都必须初始化一个起点。
        /// </summary>
        [Test]
        public void ValidateLastFPInitialization_RejectsUnitWithoutStart()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
            };
            int[] lastFP =
            {
                -1,
            };

            Assert.Throws<InvalidOperationException>(() => CircleGenerator.ValidateLastFPInitialization(cudaPoints, lastFP));
        }

        /// <summary>
        /// 备注：指定当前批次数量时，lastFP.Length 必须严格等于 pointCount * batchStartPointCount。
        /// </summary>
        [Test]
        public void ValidateLastFPInitialization_RejectsLengthThatDoesNotMatchBatchCount()
        {
            int[] cudaPoints =
            {
                0, 0, CircleGenerator.HarvestPointCode,
                1, 2, CircleGenerator.PurchasePointCode,
            };
            int[] lastFP =
            {
                0,
                -1,
            };

            Assert.Throws<ArgumentException>(() =>
                CircleGenerator.ValidateLastFPInitialization(cudaPoints, lastFP, 2));
        }

        /// <summary>
        /// 备注：当前批次的起点范围不能超过 tradingPoints 的数量。
        /// </summary>
        [Test]
        public void ValidateCudaBatchRange_RejectsRangeOutsideTradingPoints()
        {
            int[] tradingPoints =
            {
                0,
                2,
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaBatchRange(tradingPoints, 1, 2));
        }

        /// <summary>
        /// 备注：当前批次至少要包含一个起点。
        /// </summary>
        [Test]
        public void ValidateCudaBatchRange_RejectsNonPositiveBatchCount()
        {
            int[] tradingPoints =
            {
                0,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CircleGenerator.ValidateCudaBatchRange(tradingPoints, 0, 0));
        }

        /// <summary>
        /// 备注：如果 CUDA 内核按 lastFP 同下标访问 passedLength，则两者长度必须完全一致。
        /// </summary>
        [Test]
        public void ValidatePassedLength_RejectsLengthDifferentFromLastFP()
        {
            int[] lastFP =
            {
                0,
                -1,
            };
            int[] passedLength =
            {
                0,
            };

            Assert.Throws<ArgumentException>(() => CircleGenerator.ValidatePassedLength(lastFP, passedLength));
        }

        /// <summary>
        /// 备注：统一 CUDA 入参包校验要求 cudaPoints 三元组内容必须和排序后的派生点完全一致。
        /// </summary>
        [Test]
        public void ValidateCudaPointArrayMatchesPoints_RejectsChangedTripleContent()
        {
            CircleGenerator.DerivedPoint[] points =
            {
                new(0, 0, CircleGenerator.FirstPathPointCode),
            };
            int[] cudaPoints =
            {
                0, 1, CircleGenerator.FirstPathPointCode,
            };

            Assert.Throws<InvalidOperationException>(() =>
                CircleGenerator.ValidateCudaPointArrayMatchesPoints(points, cudaPoints));
        }

        /// <summary>
        /// 备注：进入 CUDA 前应通过统一入参包校验，一次性校验点集、connect、tradingPoints、batch 范围和 lastFP。
        /// </summary>
        [Test]
        public void ValidateCudaInputPackage_AllowsCompleteValidPackage()
        {
            CircleGenerator.DerivedPoint[] points = CreateDeterministicPathPoints();
            int[] pathPointCounts = CreateDeterministicPathPointCounts();
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);
            int[] connect = CreateDeterministicPathConnect();
            int[] tradingPoints = CreateDeterministicPathTradingPoints();
            int pointCount = cudaPoints.Length / 3;
            int[] lastFP = CreateDeterministicPathInitialLastFP(pointCount);

            Assert.DoesNotThrow(() =>
                CircleGenerator.ValidateCudaInputPackage(
                    points,
                    pathPointCounts,
                    cudaPoints,
                    connect,
                    tradingPoints,
                    1,
                    2,
                    0,
                    2,
                    lastFP));
        }

        /// <summary>
        /// Remark: the CUDA wrapper validates the triple layout before invoking native code.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_RejectsNonTripleLength()
        {
            Assert.Throws<ArgumentException>(() =>
                CalpathCuda.AcceptPoints(
                    new[] { new CircleGenerator.DerivedPoint(0, 0, CircleGenerator.FirstPathPointCode) },
                    new[] { 5 },
                    new[] { 1, 2 },
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    0,
                    0,
                    0,
                    1));
        }

        /// <summary>
        /// Remark: the CUDA wrapper validates the connect/cudaPoints 1:3 ratio before invoking native code.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_RejectsMismatchedConnectLength()
        {
            CircleGenerator.DerivedPoint[] points = CreateDeterministicPathPoints();
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);
            int[] tradingPoints = CreateDeterministicPathTradingPoints();
            int[] lastFP = CreateDeterministicPathInitialLastFP(cudaPoints.Length / 3);

            Assert.Throws<ArgumentException>(() =>
                CalpathCuda.AcceptPoints(
                    points,
                    CreateDeterministicPathPointCounts(),
                    cudaPoints,
                    lastFP,
                    new[] { -1 },
                    tradingPoints,
                    1,
                    2,
                    0,
                    2));
        }

        /// <summary>
        /// Remark: lastFP stores one predecessor table per parallel start point, so its length must be pointCount * batchStartPointCount.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_RejectsMismatchedLastFPLength()
        {
            CircleGenerator.DerivedPoint[] points = CreateDeterministicPathPoints();
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);

            Assert.Throws<ArgumentException>(() =>
                CalpathCuda.AcceptPoints(
                    points,
                    CreateDeterministicPathPointCounts(),
                    cudaPoints,
                    new[] { 0, -1, -1 },
                    CreateDeterministicPathConnect(),
                    CreateDeterministicPathTradingPoints(),
                    1,
                    2,
                    0,
                    2));
        }

        /// <summary>
        /// Remark: the C# reference implementation mirrors CUDA path relaxation and records the previous point in lastFP.
        /// </summary>
        [Test]
        public void CalpathReference_CalculatesExpectedLastFP()
        {
            int[] cudaPoints = CreateDeterministicPathCudaPoints();
            int[] connect = CreateDeterministicPathConnect();
            int pointCount = cudaPoints.Length / 3;
            int[] lastFP = CreateDeterministicPathInitialLastFP(pointCount);

            int status = CalpathReference.CalculatePaths(cudaPoints, lastFP, connect);

            Assert.Multiple(() =>
            {
                Assert.That(status, Is.EqualTo(0));
                Assert.That(
                    lastFP,
                    Is.EqualTo(new[]
                    {
                        0, 0, -1, 7, 3, 1, 5, 6,
                        2, 5, 1, 7, 4, 4, 5, 6,
                    }));
            });
        }

        /// <summary>
        /// Remark: CUDA production calculation and the C# reference calculation must produce the same lastFP table.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_MatchesCSharpReference()
        {
            if (!CalpathCuda.IsAvailable)
            {
                Assert.Ignore($"CUDA DLL not found: {CalpathCuda.DllPath}");
            }

            int[] cudaPoints = CreateDeterministicPathCudaPoints();
            int[] connect = CreateDeterministicPathConnect();
            int pointCount = cudaPoints.Length / 3;
            int[] expectedLastFP = CreateDeterministicPathInitialLastFP(pointCount);
            int[] actualLastFP = CreateDeterministicPathInitialLastFP(pointCount);

            Assert.That(CalpathReference.CalculatePaths(cudaPoints, expectedLastFP, connect), Is.EqualTo(0));
            Assert.That(
                CalpathCuda.AcceptPoints(
                    CreateDeterministicPathPoints(),
                    CreateDeterministicPathPointCounts(),
                    cudaPoints,
                    actualLastFP,
                    connect,
                    CreateDeterministicPathTradingPoints(),
                    1,
                    2,
                    0,
                    2),
                Is.EqualTo(0));

            Assert.That(actualLastFP, Is.EqualTo(expectedLastFP));
        }
        /// <summary>
        /// Remark: the public CUDA wrapper must reject incomplete business context before the native DLL is called.
        /// </summary>
        [Test]
        public void CalpathCuda_AcceptPoints_RejectsEmptyBusinessPackage()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CalpathCuda.AcceptPoints(
                    Array.Empty<CircleGenerator.DerivedPoint>(),
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    0,
                    0,
                    0,
                    1));
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
                string normalizedDxf = dxf.Replace("\r\n", "\n");
                int circle0BlockStart = normalizedDxf.IndexOf("Circle_0", StringComparison.Ordinal);
                int circle0BlockEnd = circle0BlockStart >= 0
                    ? normalizedDxf.IndexOf("ENDBLK", circle0BlockStart, StringComparison.Ordinal)
                    : -1;
                string circle0Block = circle0BlockStart >= 0 && circle0BlockEnd > circle0BlockStart
                    ? normalizedDxf.Substring(circle0BlockStart, circle0BlockEnd - circle0BlockStart)
                    : string.Empty;

                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(outputPath), Is.True);
                    Assert.That(dxf, Does.Contain("ShortCircle"));
                    Assert.That(dxf, Does.Contain("LongCircle"));
                    Assert.That(dxf, Does.Contain("OrdinaryConnect"));
                    Assert.That(dxf, Does.Contain("TerminalConnect"));
                    Assert.That(dxf, Does.Contain("PurchaseArrow"));
                    Assert.That(circle0BlockStart, Is.GreaterThanOrEqualTo(0));
                    Assert.That(circle0BlockEnd, Is.GreaterThan(circle0BlockStart));
                    Assert.That(circle0Block, Does.Contain("PurchaseArrow"));
                    Assert.That(circle0Block, Does.Contain("62\n30"));
                    Assert.That(dxf, Does.Contain("BLOCK"));
                    Assert.That(dxf, Does.Contain("ENDBLK"));
                    Assert.That(dxf, Does.Contain("INSERT"));
                    Assert.That(dxf, Does.Contain("Circle_0"));
                    Assert.That(dxf, Does.Contain("Circle_1"));
                    Assert.That(dxf, Does.Contain("LINE"));
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
        /// Remark: route DXF uses direct entities only, avoiding BLOCK/INSERT, transparency and lineweight compatibility issues in CAD.
        /// </summary>
        [Test]
        public void DrawRouteToDxf_WritesCadSafeDirectEntities()
        {
            string inputPath = CreateTemporaryCircleFilePath();
            string outputPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{Guid.NewGuid():N}-Route.dxf");
            WriteCircleFile(
                inputPath,
                new CircleGenerator.CircleRecord(-400_000, 0, -499_999),
                new CircleGenerator.CircleRecord(400_000, 0, 499_999));

            try
            {
                CircleGenerator.DrawRouteToDxf(inputPath, outputPath, new[] { 0 });
                string dxf = File.ReadAllText(outputPath);
                string normalizedDxf = dxf.Replace("\r\n", "\n");

                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(outputPath), Is.True);
                    Assert.That(normalizedDxf, Does.Not.Contain("\nBLOCK\n"));
                    Assert.That(normalizedDxf, Does.Not.Contain("\nENDBLK\n"));
                    Assert.That(normalizedDxf, Does.Not.Contain("\nINSERT\n"));
                    Assert.That(normalizedDxf, Does.Not.Contain("\n370\n"));
                    Assert.That(normalizedDxf, Does.Not.Contain("\n440\n"));
                    Assert.That(normalizedDxf, Does.Contain("\nCIRCLE\n"));
                    Assert.That(normalizedDxf, Does.Contain("\nTEXT\n"));
                    Assert.That(normalizedDxf, Does.Contain("RoutePath"));
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
        /// Remark: DXF ARC is always counter-clockwise, so clockwise route arcs must use signed geometry angles.
        /// </summary>
        [Test]
        public void CalculateDxfRouteArcAngles_UsesSignedDirection()
        {
            CircleGenerator.CircleRecord longCircle = new(0, 0, 10);
            CircleGenerator.CircleRecord shortCircle = new(0, 0, -10);

            (double longStartAngle, double longEndAngle) =
                CircleGenerator.CalculateDxfRouteArcAngles(longCircle, 360, 0, 1);
            (double shortStartAngle, double shortEndAngle) =
                CircleGenerator.CalculateDxfRouteArcAngles(shortCircle, 360, 0, 1);

            Assert.Multiple(() =>
            {
                Assert.That(longStartAngle, Is.EqualTo(0.0).Within(1e-9));
                Assert.That(longEndAngle, Is.EqualTo(1.0).Within(1e-9));
                Assert.That(CalculateDxfCounterClockwiseSweep(longStartAngle, longEndAngle), Is.EqualTo(1.0).Within(1e-9));

                Assert.That(shortStartAngle, Is.EqualTo(359.0).Within(1e-9));
                Assert.That(shortEndAngle, Is.EqualTo(0.0).Within(1e-9));
                Assert.That(CalculateDxfCounterClockwiseSweep(shortStartAngle, shortEndAngle), Is.EqualTo(1.0).Within(1e-9));
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

        private static HashSet<(int CircleIndex, int PointIndex)> BuildUnavailableOccupyingPointsBeforePurchase(
            IReadOnlyList<CircleGenerator.DerivedPoint> points)
        {
            return points
                .Where(point =>
                    point.PointType % CircleGenerator.HarvestPointCode == 0 ||
                    point.PointType % CircleGenerator.TerminalLinkPointCode == 0 ||
                    point.PointType % CircleGenerator.OrdinaryLinkPointCode == 0)
                .Select(point => (point.CircleIndex, point.PointIndex))
                .ToHashSet();
        }

        private static List<(int CircleIndex, int PointIndex)> CalculateExpectedPurchasePointsByBruteForce(
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts,
            HashSet<(int CircleIndex, int PointIndex)> unavailablePoints)
        {
            HashSet<(int CircleIndex, int PointIndex)> occupiedPoints = new(unavailablePoints);
            List<(int CircleIndex, int PointIndex)> purchasePoints = new();

            for (int targetCircleIndex = 0; targetCircleIndex < circles.Count; targetCircleIndex++)
            {
                CircleGenerator.CircleRecord targetCircle = circles[targetCircleIndex];
                (int CircleIndex, int PointIndex)? bestPoint = null;
                double bestDistanceSquared = 0.0;

                for (int candidateCircleIndex = 0; candidateCircleIndex < circles.Count; candidateCircleIndex++)
                {
                    CircleGenerator.CircleRecord candidateCircle = circles[candidateCircleIndex];
                    int pathPointCount = pathPointCounts[candidateCircleIndex];

                    for (int pointIndex = 0; pointIndex < pathPointCount; pointIndex++)
                    {
                        (int CircleIndex, int PointIndex) candidatePoint = (candidateCircleIndex, pointIndex);

                        if (occupiedPoints.Contains(candidatePoint))
                        {
                            continue;
                        }

                        (double x, double y) = CircleGenerator.CalculatePathPointCoordinate(
                            candidateCircle,
                            pointIndex,
                            pathPointCount);
                        double dx = x - targetCircle.A;
                        double dy = y - targetCircle.B;
                        double distanceSquared = dx * dx + dy * dy;

                        if (bestPoint is null ||
                            distanceSquared < bestDistanceSquared - 1e-7 ||
                            (Math.Abs(distanceSquared - bestDistanceSquared) <= 1e-7 &&
                             IsEarlierPathPoint(candidatePoint, bestPoint.Value)))
                        {
                            bestPoint = candidatePoint;
                            bestDistanceSquared = distanceSquared;
                        }
                    }
                }

                if (bestPoint is null)
                {
                    throw new InvalidOperationException($"No expected purchase point for circle {targetCircleIndex}.");
                }

                occupiedPoints.Add(bestPoint.Value);
                purchasePoints.Add(bestPoint.Value);
            }

            return purchasePoints;
        }

        private static bool IsEarlierPathPoint(
            (int CircleIndex, int PointIndex) candidate,
            (int CircleIndex, int PointIndex) currentBest)
        {
            if (candidate.CircleIndex != currentBest.CircleIndex)
            {
                return candidate.CircleIndex < currentBest.CircleIndex;
            }

            return candidate.PointIndex < currentBest.PointIndex;
        }
        private static CircleGenerator.DerivedPoint[] CreateDeterministicPathPoints()
        {
            return new[]
            {
                new CircleGenerator.DerivedPoint(0, 0, CircleGenerator.FirstPathPointCode * CircleGenerator.PurchasePointCode),
                new CircleGenerator.DerivedPoint(0, 10, CircleGenerator.OrdinaryLinkPointCode),
                new CircleGenerator.DerivedPoint(0, 20, CircleGenerator.TerminalLinkPointCode),
                new CircleGenerator.DerivedPoint(1, 0, CircleGenerator.FirstPathPointCode),
                new CircleGenerator.DerivedPoint(1, 1, CircleGenerator.PurchasePointCode),
                new CircleGenerator.DerivedPoint(1, 5, CircleGenerator.OrdinaryLinkPointCode),
                new CircleGenerator.DerivedPoint(1, 9, CircleGenerator.HarvestPointCode),
                new CircleGenerator.DerivedPoint(1, 12, CircleGenerator.TerminalLinkPointCode),
            };
        }

        private static int[] CreateDeterministicPathPointCounts()
        {
            return new[]
            {
                21,
                13,
            };
        }

        private static int[] CreateDeterministicPathCudaPoints()
        {
            return CircleGenerator.BuildCudaPointArray(CreateDeterministicPathPoints());
        }

        private static int[] CreateDeterministicPathConnect()
        {
            return new[]
            {
                -1,
                5,
                0,
                -1,
                -1,
                1,
                -1,
                3,
            };
        }

        private static int[] CreateDeterministicPathTradingPoints()
        {
            return new[]
            {
                0,
                4,
                6,
            };
        }

        private static int[] CreateDeterministicPathInitialLastFP(int pointCount)
        {
            int[] lastFP = Enumerable.Repeat(-1, pointCount * 2).ToArray();
            lastFP[0] = 0;
            lastFP[pointCount + 4] = 4;
            return lastFP;
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

        private static double CalculateDxfCounterClockwiseSweep(double startAngle, double endAngle)
        {
            double sweep = endAngle - startAngle;
            return sweep < 0.0 ? sweep + 360.0 : sweep;
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
