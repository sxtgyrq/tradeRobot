using System.Reflection;
using System.Runtime.ExceptionServices;
using ConsoleMain;
using KChartRunWithFiveElements;

namespace TestFiveElement;

internal static class TestHelpers
{
    private static readonly Type ProgramType = typeof(Program);

    public static KLine Line(
        decimal open = 100m,
        decimal high = 101m,
        decimal low = 99m,
        decimal close = 100m,
        int hour = 0,
        decimal volume = 1m)
    {
        return new KLine(
            new DateTime(2026, 1, 1, 0, 0, 0).AddHours(hour),
            open,
            high,
            low,
            close,
            volume);
    }

    public static KChartTradingPointInfo TradingPoint(
        KChartTradingPointKind kind,
        int tradingPointIndex,
        int cudaPointIndex,
        int ownerCircleIndex,
        int pointCircleIndex,
        int pointIndex = 0,
        string? ownerCircleId = null,
        string? pointCircleId = null)
    {
        return new KChartTradingPointInfo(
            kind,
            tradingPointIndex,
            cudaPointIndex,
            ownerCircleIndex,
            ownerCircleId ?? CircleId(ownerCircleIndex),
            pointCircleIndex,
            pointCircleId ?? CircleId(pointCircleIndex),
            pointIndex);
    }

    public static KChartTradingPointInfo HarvestTradingPoint(
        int tradingPointIndex = 0,
        int cudaPointIndex = 0,
        int pointCircleIndex = 0,
        int pointIndex = 0)
    {
        return TradingPoint(
            KChartTradingPointKind.Harvest,
            tradingPointIndex,
            cudaPointIndex,
            -1,
            pointCircleIndex,
            pointIndex,
            "000000000000000000000000",
            CircleId(pointCircleIndex));
    }

    public static KChartTradingPointInfo PurchaseTradingPoint(
        int ownerCircleIndex,
        int tradingPointIndex = 1,
        int cudaPointIndex = 1,
        int pointCircleIndex = -1,
        int pointIndex = 0)
    {
        int actualPointCircleIndex = pointCircleIndex < 0 ? ownerCircleIndex : pointCircleIndex;
        return TradingPoint(
            KChartTradingPointKind.Purchase,
            tradingPointIndex,
            cudaPointIndex,
            ownerCircleIndex,
            actualPointCircleIndex,
            pointIndex);
    }

    public static TradingPointAccount HarvestAccount(long satoshi = 210_000_000_000L, decimal u = 1000m)
    {
        return new TradingPointAccount(HarvestTradingPoint(), satoshi, u);
    }

    public static TradingPointAccount PurchaseAccount(
        int ownerCircleIndex = 0,
        long satoshi = 0,
        decimal u = 1000m,
        int tradingPointIndex = 1,
        int cudaPointIndex = 1,
        int pointCircleIndex = -1)
    {
        return new TradingPointAccount(
            PurchaseTradingPoint(
                ownerCircleIndex,
                tradingPointIndex,
                cudaPointIndex,
                pointCircleIndex),
            satoshi,
            u);
    }

    public static string CircleId(int circleIndex)
    {
        if (circleIndex < 0)
        {
            return "000000000000000000000000";
        }

        return (circleIndex + 1).ToString("x24");
    }

    public static KChartRoutePointInfo RoutePoint(
        int cudaPointIndex,
        int circleIndex,
        int pointIndex,
        int pathPointCount = 100,
        int signedRadius = 100)
    {
        return new KChartRoutePointInfo(
            cudaPointIndex,
            circleIndex,
            CircleId(circleIndex),
            pointIndex,
            1,
            pathPointCount,
            signedRadius);
    }

    public static KChartRouteInfo Route(params KChartRoutePointInfo[] routePoints)
    {
        return new KChartRouteInfo(
            0,
            1,
            routePoints.First().CudaPointIndex,
            routePoints.Last().CudaPointIndex,
            routePoints.Select(item => item.CudaPointIndex).ToArray(),
            routePoints);
    }

    public static SpotOrder SpotOrder(
        TradingPointAccount account,
        SpotOrderSide side,
        long satoshiAmount = 100,
        decimal uAmount = 1m,
        int createdKLineIndex = 1,
        int availableFromKLineIndex = 2,
        FiveElement fiveElement = FiveElement.Metal,
        decimal price = 100m)
    {
        return new SpotOrder(
            account,
            side,
            satoshiAmount,
            uAmount,
            0.1m,
            createdKLineIndex,
            availableFromKLineIndex,
            fiveElement,
            price);
    }

    public static ContractOrder ContractOrder(
        TradingPointAccount ownerAccount,
        ContractDirection direction = ContractDirection.Long,
        ContractMarginAsset marginAsset = ContractMarginAsset.U,
        decimal marginAmount = 10m,
        decimal price = 100m,
        decimal leverage = 2m,
        int createdKLineIndex = 1,
        int availableFromKLineIndex = 2,
        FiveElement fiveElement = FiveElement.Metal)
    {
        decimal takeProfitPrice = direction == ContractDirection.Long ? 110m : 90m;
        decimal liquidationPrice = direction == ContractDirection.Long ? 50m : 150m;

        return new ContractOrder(
            ownerAccount.TradingPoint,
            HarvestTradingPoint(),
            ownerAccount.TradingPoint.OwnerCircleIndex,
            ownerAccount.TradingPoint.OwnerCircleId,
            ownerAccount.TradingPoint.OwnerCircleIndex,
            ownerAccount.TradingPoint.OwnerCircleId,
            direction,
            marginAsset,
            marginAmount,
            0.1m,
            10,
            0.1m,
            100,
            createdKLineIndex,
            availableFromKLineIndex,
            fiveElement,
            price,
            leverage,
            takeProfitPrice,
            liquidationPrice,
            marginAmount * leverage,
            new[] { 0, 1 });
    }

    public static object? InvokeProgram(string methodName, params object?[] args)
    {
        MethodInfo[] methods = ProgramType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(item => item.Name == methodName && item.GetParameters().Length == args.Length)
            .ToArray();

        if (methods.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one private Program method named {methodName} with {args.Length} parameters, found {methods.Length}.");
        }

        try
        {
            return methods[0].Invoke(null, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    public static T InvokeProgram<T>(string methodName, params object?[] args)
    {
        return (T)InvokeProgram(methodName, args)!;
    }

    public static T GetProperty<T>(object value, string propertyName)
    {
        PropertyInfo property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");
        return (T)property.GetValue(value)!;
    }

    public static string CaptureConsole(Action action, string? input = null)
    {
        TextWriter oldOutput = Console.Out;
        TextReader oldInput = Console.In;
        using StringWriter output = new StringWriter();
        using StringReader? reader = input is null ? null : new StringReader(input);

        Console.SetOut(output);
        if (reader is not null)
        {
            Console.SetIn(reader);
        }

        try
        {
            action();
        }
        finally
        {
            Console.SetOut(oldOutput);
            Console.SetIn(oldInput);
        }

        return output.ToString();
    }
}
