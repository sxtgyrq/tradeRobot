using ConsoleMain;
using System.Reflection;

namespace TestFiveElement;

internal class KChartRunWithFiveElementsApiTests
{
    private const string CircleFileName = "Circle.bin";

    [Test]
    public void ReplaceBankruptCirclesAndRecalculateRoute_WithNoIndexesReportsCurrentCircleCount()
    {
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-api").FullName;
        string oldDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = tempDirectory;
            string circlePath = Path.Combine(tempDirectory, CircleFileName);
            WriteCircleRecords(
                circlePath,
                (0, 0, 10),
                (10, 0, -10));

            KChartCircleMaintenanceResult result =
                KChartRunWithFiveElementsAPI.ReplaceBankruptCirclesAndRecalculateRoute(Array.Empty<int>());

            Assert.Multiple(() =>
            {
                Assert.That(result.CirclePath, Is.EqualTo(circlePath));
                Assert.That(result.OriginalCircleCount, Is.EqualTo(2));
                Assert.That(result.RemovedCircleCount, Is.Zero);
                Assert.That(result.AddedCircleCount, Is.Zero);
                Assert.That(result.FinalCircleCount, Is.EqualTo(2));
                Assert.That(result.RemovedCircleIndexes, Is.Empty);
                Assert.That(result.AddedCircleIds, Is.Empty);
            });
        }
        finally
        {
            Environment.CurrentDirectory = oldDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void ReplaceBankruptCirclesAndRecalculateRoute_WithNoIndexesRejectsCorruptedCircleFile()
    {
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-api-corrupt").FullName;
        string oldDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = tempDirectory;
            string circlePath = Path.Combine(tempDirectory, CircleFileName);
            File.WriteAllBytes(circlePath, new byte[] { 1, 2, 3 });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                KChartRunWithFiveElementsAPI.ReplaceBankruptCirclesAndRecalculateRoute(Array.Empty<int>()))!;

            Assert.That(exception.Message, Does.Contain("multiple of 12"));
        }
        finally
        {
            Environment.CurrentDirectory = oldDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void ReplaceBankruptCirclesAndRecalculateRoute_RejectsInvalidIndexesBeforeChangingFiles()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                KChartRunWithFiveElementsAPI.ReplaceBankruptCirclesAndRecalculateRoute(new[] { -1 }));

            InvalidOperationException duplicateException = Assert.Throws<InvalidOperationException>(() =>
                KChartRunWithFiveElementsAPI.ReplaceBankruptCirclesAndRecalculateRoute(new[] { 1, 1 }))!;
            Assert.That(duplicateException.Message, Does.Contain("Duplicate bankrupt circle index"));
        });
    }

    [Test]
    public void RouteFileHelpers_ReadTradingPointRowAndBacktrackByCudaPointIndex()
    {
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-route-row").FullName;
        try
        {
            string routePath = Path.Combine(tempDirectory, "testRoute.bin");

            // route.bin 的格式是“交易点起点行 * CUDA 点列”：
            // 第 0 行表示第 0 个交易点作为起点时，各 CUDA 点的上一个路径点；
            // 第 1 行表示第 1 个交易点作为起点时，各 CUDA 点的上一个路径点。
            int[] row0 =
            {
                0, 0, 1, 2, -1
            };
            int[] row1 =
            {
                -1, 2, 3, 4, 4
            };

            using (FileStream stream = File.Create(routePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                foreach (int value in row0.Concat(row1))
                {
                    writer.Write(value);
                }
            }

            int[] loadedRow = InvokeConsoleProgram<int[]>(
                "ReadRouteRow",
                routePath,
                1,
                2,
                5);
            List<int> routePointIndexes = InvokeConsoleProgram<List<int>>(
                "BuildRoutePointIndexes",
                loadedRow,
                4,
                1);

            Assert.Multiple(() =>
            {
                Assert.That(loadedRow, Is.EqualTo(row1));
                Assert.That(routePointIndexes, Is.EqualTo(new[] { 4, 3, 2, 1 }));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static void WriteCircleRecords(
        string circlePath,
        params (int A, int B, int SignedRadius)[] circles)
    {
        using FileStream stream = File.Create(circlePath);
        using BinaryWriter writer = new BinaryWriter(stream);
        foreach ((int a, int b, int signedRadius) in circles)
        {
            writer.Write(a);
            writer.Write(b);
            writer.Write(signedRadius);
        }
    }

    private static T InvokeConsoleProgram<T>(string methodName, params object[] args)
    {
        Type programType = typeof(KChartRunWithFiveElementsAPI).Assembly.GetType("ConsoleMain.Program")
            ?? throw new InvalidOperationException("ConsoleMain.Program type was not found.");
        MethodInfo method = programType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"ConsoleMain.Program.{methodName} was not found.");

        try
        {
            return (T)method.Invoke(null, args)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
