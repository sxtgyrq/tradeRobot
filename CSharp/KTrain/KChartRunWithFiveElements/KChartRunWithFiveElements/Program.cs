using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CommonClass;
using ConsoleMain;
using DalOfAddress;

namespace KChartRunWithFiveElements;

internal class Program
{
	private enum ContractKLineResult
	{
		None,
		TakeProfit,
		Liquidation
	}

	private readonly struct HarvestFeePaymentResult
	{
		public bool IsPaid { get; }

		public long ReturnableSatoshi { get; }

		public IReadOnlyList<SatoshiTransfer> SatoshiTransfers { get; }

		public HarvestFeePaymentResult(bool isPaid, long returnableSatoshi, IReadOnlyList<SatoshiTransfer>? satoshiTransfers = null)
		{
			IsPaid = isPaid;
			ReturnableSatoshi = returnableSatoshi;
			SatoshiTransfers = satoshiTransfers?.ToArray() ?? Array.Empty<SatoshiTransfer>();
		}
	}

	private readonly struct HarvestResult
	{
		public IReadOnlyList<int> BankruptOwnerCircleIndexes { get; }

		public IReadOnlyList<SatoshiTransfer> SatoshiTransfers { get; }

		public int BankruptCount => BankruptOwnerCircleIndexes.Count;

		public HarvestResult(IReadOnlyList<int> bankruptOwnerCircleIndexes, IReadOnlyList<SatoshiTransfer>? satoshiTransfers = null)
		{
			BankruptOwnerCircleIndexes = bankruptOwnerCircleIndexes?.ToArray() ?? throw new ArgumentNullException("bankruptOwnerCircleIndexes");
			SatoshiTransfers = satoshiTransfers?.ToArray() ?? Array.Empty<SatoshiTransfer>();
		}
	}

	private readonly struct SatoshiTransfer
	{
		public TradingPointAccount SourceAccount { get; }

		public TradingPointAccount TargetAccount { get; }

		public long SatoshiAmount { get; }

		public SatoshiTransfer(TradingPointAccount sourceAccount, TradingPointAccount targetAccount, long satoshiAmount)
		{
			SourceAccount = sourceAccount ?? throw new ArgumentNullException("sourceAccount");
			TargetAccount = targetAccount ?? throw new ArgumentNullException("targetAccount");
			if (satoshiAmount <= 0)
			{
				throw new ArgumentOutOfRangeException("satoshiAmount");
			}
			SatoshiAmount = satoshiAmount;
		}
	}

	private const string StopFileName = "stop.bin";

	private const long InitialHarvestSatoshiBalance = 210000000000L;

	private const long InitialPurchaseSatoshiBalance = 0L;

	private const decimal InitialTradingPointUBalance = 1000m;

	private const decimal SatoshisPerBitcoin = 100000000m;

	private const decimal HarvestReserveRatio = 0.12m;

	private const decimal LiquidityShortageRatio = 0.03m;

	private const decimal LiquidityInjectionRatio = 0.12m;

	private const decimal DistributionRatio = 0.20m;

	private const decimal ContractLeverage = 2.71828m;

	private const decimal TwoPi = 6.2831853071795864769252867666m;

	private const long HarvestFeeSatoshi = 100L;

	private static void Main(string[] args)
	{
		Console.WriteLine("你好，欢迎来到K线的五行世界。");
		Connection.SetPassWord("");
		List<TradingPointAccount>? list = null;
		List<ContractOrder> list2 = new List<ContractOrder>();
		List<SpotOrder> list3 = new List<SpotOrder>();
		Dictionary<TradingPointAccount, long> rebuiltPendingHarvestReturns = new Dictionary<TradingPointAccount, long>();
		int num = 24;
		while (RunLoop())
		{
			List<dataItem> all = DALHourRecord.GetAll();
			dataItem dataItem = all[all.Count - 1];
			Console.WriteLine($"allData.Count={all.Count}");
			IReadOnlyList<KLine> readOnlyList = ConvertToKLines(all);
			KChartRunWithFiveElementsAPI.GenerateCircleAndRoute();
			KChartRunWithFiveElementsAPI.CalculateRouteForExistingCircle();
			IReadOnlyList<KChartTradingPointInfo> readOnlyList2 = KChartRunWithFiveElementsAPI.LoadTradingPoints();
			list = ((list != null) ? RebuildTradingPointAccountsAfterCircleMaintenance(list, readOnlyList2, list3, list2, null, rebuiltPendingHarvestReturns, out rebuiltPendingHarvestReturns) : CreateTradingPointAccounts(readOnlyList2));
			int value = list.Count((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
			int value2 = list.Count((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Purchase);
			Console.WriteLine($"Trading point account count={list.Count}");
			Console.WriteLine($"Harvest account count={value}, purchase account count={value2}");
			long before = CalculateTrackedSatoshiTotal(list);
			for (int i = 0; i < all.Count - 1; i++)
			{
				if (i >= 24 && i >= num)
				{
					KLineFiveElementResult kLineFiveElementResult = FiveElementClassifier.ClassifyNext(readOnlyList, i - 24);
					FiveElement element = kLineFiveElementResult.Element;
					Console.WriteLine($"当前五行为：{FiveElementDisplay.ToChineseName(element)}({element})");
					if (i % 24 == 0)
					{
						long before2 = CalculateTrackedSatoshiTotal(list);
						IReadOnlyList<SatoshiTransfer> satoshiTransfers = ReturnHarvestedSatoshi(i, list, rebuiltPendingHarvestReturns);
						AddContractOrdersForSatoshiTransfers(satoshiTransfers, i, element, readOnlyList[i].OpenValue, list, list2);
						EnsureSatoshiTotalUnchanged("return harvested satoshi", before2, list);
						long before3 = CalculateTrackedSatoshiTotal(list);
						ExecuteDistribution(i, all[23].closeValue, element, list, list2, CalculatePendingHarvestReturnTotal(rebuiltPendingHarvestReturns));
						EnsureSatoshiTotalUnchanged("distribution", before3, list);
					}
					long before4 = CalculateTrackedSatoshiTotal(list);
					SpotMarket(i, readOnlyList[i], element, list, list3, list2);
					EnsureSatoshiTotalUnchanged("spot market", before4, list);
					before4 = CalculateTrackedSatoshiTotal(list);
					MatchingTrade(i, readOnlyList[i], element, list, list3, list2);
					EnsureSatoshiTotalUnchanged("matching trade", before4, list);
					before4 = CalculateTrackedSatoshiTotal(list);
					HarvestResult harvestResult = Harvest(i, readOnlyList[i], list, list3, list2, rebuiltPendingHarvestReturns);
					AddContractOrdersForSatoshiTransfers(harvestResult.SatoshiTransfers, i, element, readOnlyList[i].CloseValue, list, list2);
					EnsureSatoshiTotalUnchanged("harvest and bankruptcy liquidation", before4, list);
					if (harvestResult.BankruptCount > 0)
					{
						InjectLiquidityToHarvest(i, readOnlyList[i], list);
						KChartCircleMaintenanceResult kChartCircleMaintenanceResult = KChartRunWithFiveElementsAPI.ReplaceBankruptCirclesAndRecalculateRoute(harvestResult.BankruptOwnerCircleIndexes);
						IReadOnlyList<KChartTradingPointInfo> reloadedTradingPoints = KChartRunWithFiveElementsAPI.LoadTradingPoints();
						list = RebuildTradingPointAccountsAfterCircleMaintenance(list, reloadedTradingPoints, list3, list2, readOnlyList[i], rebuiltPendingHarvestReturns, out rebuiltPendingHarvestReturns);
						Console.WriteLine($"Circle maintenance done: removed={kChartCircleMaintenanceResult.RemovedCircleCount}, added={kChartCircleMaintenanceResult.AddedCircleCount}, final={kChartCircleMaintenanceResult.FinalCircleCount}.");
					}
					EnsureSatoshiTotalUnchanged($"k-line loop end index={i}", before, list);
				}
			}
			num = Math.Max(num, all.Count - 1);
			Console.ReadLine();
		}
	}

	private static void InjectLiquidityToHarvest(int kLineIndex, KLine currentKLine, List<TradingPointAccount> tradingPointAccounts)
	{
		if (kLineIndex % 24 != 23)
		{
			return;
		}
		if (currentKLine == null)
		{
			throw new ArgumentNullException("currentKLine");
		}
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (currentKLine.CloseValue <= 0m)
		{
			throw new ArgumentOutOfRangeException("currentKLine", "Close price must be greater than 0.");
		}
		TradingPointAccount tradingPointAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		decimal num = tradingPointAccounts.Sum((TradingPointAccount item) => CalculateAccountTotalAssetInSatoshi(item, currentKLine.CloseValue));
		if (num <= 0m)
		{
			Console.WriteLine("Liquidity injection skipped: system total asset is not positive.");
			return;
		}
		decimal num2 = CalculateAccountTotalAssetInSatoshi(tradingPointAccount, currentKLine.CloseValue);
		decimal num3 = num * 0.03m;
		if (!(num2 >= num3))
		{
			decimal num4 = num * 0.12m;
			decimal num5 = ConvertSatoshiValueToU(num4, currentKLine.CloseValue);
			tradingPointAccount.UBalance += num5;
			Console.WriteLine($"Liquidity injected to harvest: u={num5}, satoshiValue={num4}, harvestAssetBefore={num2}, systemAssetBefore={num}.");
		}
	}

	private static HarvestResult Harvest(int kLineIndex, KLine currentKLine, List<TradingPointAccount> tradingPointAccounts, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket, Dictionary<TradingPointAccount, long> pendingHarvestReturns)
	{
		if (kLineIndex % 24 != 23)
		{
			return new HarvestResult(Array.Empty<int>());
		}
		if (currentKLine == null)
		{
			throw new ArgumentNullException("currentKLine");
		}
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (spotMarket == null)
		{
			throw new ArgumentNullException("spotMarket");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		if (pendingHarvestReturns == null)
		{
			throw new ArgumentNullException("pendingHarvestReturns");
		}
		TradingPointAccount tradingPointAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		List<TradingPointAccount> list = tradingPointAccounts.Where((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Purchase && !item.IsBankrupt).ToList();
		int num = 0;
		List<int> list2 = new List<int>();
		List<SatoshiTransfer> list3 = new List<SatoshiTransfer>();
		foreach (TradingPointAccount item in list)
		{
			HarvestFeePaymentResult harvestFeePaymentResult = TryPayHarvestFee(item, tradingPointAccount, currentKLine.CloseValue, spotMarket, contractMarket);
			if (harvestFeePaymentResult.IsPaid)
			{
				if (harvestFeePaymentResult.ReturnableSatoshi > 0)
				{
					AddPendingHarvestReturn(pendingHarvestReturns, item, harvestFeePaymentResult.ReturnableSatoshi);
				}
				list3.AddRange(harvestFeePaymentResult.SatoshiTransfers);
				num++;
			}
			else
			{
				LiquidateBankruptAccount(item, tradingPointAccount, spotMarket, contractMarket, currentKLine);
				list2.Add(item.TradingPoint.OwnerCircleIndex);
			}
		}
		Console.WriteLine($"Harvest: fee={100}, paid={num}, bankrupt={list2.Count}, harvestSatoshi={tradingPointAccount.SatoshiBalance}.");
		return new HarvestResult(list2, list3);
	}

	private static void AddPendingHarvestReturn(Dictionary<TradingPointAccount, long> pendingHarvestReturns, TradingPointAccount purchaseAccount, long satoshiAmount)
	{
		if (pendingHarvestReturns == null)
		{
			throw new ArgumentNullException("pendingHarvestReturns");
		}
		if (purchaseAccount == null)
		{
			throw new ArgumentNullException("purchaseAccount");
		}
		if (satoshiAmount <= 0)
		{
			throw new ArgumentOutOfRangeException("satoshiAmount");
		}
		if (pendingHarvestReturns.TryGetValue(purchaseAccount, out var value))
		{
			pendingHarvestReturns[purchaseAccount] = checked(value + satoshiAmount);
		}
		else
		{
			pendingHarvestReturns.Add(purchaseAccount, satoshiAmount);
		}
	}

	private static IReadOnlyList<SatoshiTransfer> ReturnHarvestedSatoshi(int kLineIndex, List<TradingPointAccount> tradingPointAccounts, Dictionary<TradingPointAccount, long> pendingHarvestReturns)
	{
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (pendingHarvestReturns == null)
		{
			throw new ArgumentNullException("pendingHarvestReturns");
		}
		if (pendingHarvestReturns.Count == 0)
		{
			return Array.Empty<SatoshiTransfer>();
		}
		TradingPointAccount tradingPointAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		long num = 0L;
		List<SatoshiTransfer> list = new List<SatoshiTransfer>();
		foreach (KeyValuePair<TradingPointAccount, long> item in pendingHarvestReturns.ToList())
		{
			TradingPointAccount key = item.Key;
			long value = item.Value;
			if (key.IsBankrupt || value <= 0)
			{
				pendingHarvestReturns.Remove(key);
				continue;
			}
			if (tradingPointAccount.SatoshiBalance < value)
			{
				throw new InvalidOperationException($"Harvest account does not have enough satoshi to return harvested satoshi. Required={value}, Available={tradingPointAccount.SatoshiBalance}.");
			}
			tradingPointAccount.SatoshiBalance -= value;
			key.SatoshiBalance += value;
			list.Add(new SatoshiTransfer(tradingPointAccount, key, value));
			num = checked(num + value);
			pendingHarvestReturns.Remove(key);
		}
		if (num > 0)
		{
			Console.WriteLine($"Harvest satoshi returned: kLineIndex={kLineIndex}, total={num}, harvestSatoshi={tradingPointAccount.SatoshiBalance}.");
		}
		return list;
	}

	private static long CalculatePendingHarvestReturnTotal(IReadOnlyDictionary<TradingPointAccount, long> pendingHarvestReturns)
	{
		if (pendingHarvestReturns == null)
		{
			throw new ArgumentNullException("pendingHarvestReturns");
		}
		long num = 0L;
		foreach (KeyValuePair<TradingPointAccount, long> pendingHarvestReturn in pendingHarvestReturns)
		{
			if (!pendingHarvestReturn.Key.IsBankrupt && pendingHarvestReturn.Value > 0)
			{
				num = checked(num + pendingHarvestReturn.Value);
			}
		}
		return num;
	}

	private static HarvestFeePaymentResult TryPayHarvestFee(TradingPointAccount purchaseAccount, TradingPointAccount harvestAccount, decimal closePrice, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket)
	{
		if (closePrice <= 0m)
		{
			throw new ArgumentOutOfRangeException("closePrice");
		}
		if (spotMarket == null)
		{
			throw new ArgumentNullException("spotMarket");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		decimal num = CalculateHarvestPayableSatoshiValue(purchaseAccount, closePrice);
		if (num < 100m)
		{
			return new HarvestFeePaymentResult(isPaid: false, 0L);
		}
		long remainingFee = 100L;
		long returnableSatoshi = 0L;
		List<SatoshiTransfer> satoshiTransfers = new List<SatoshiTransfer>();
		remainingFee = PayHarvestFeeFromAvailableBalances(purchaseAccount, harvestAccount, remainingFee, closePrice, ref returnableSatoshi, satoshiTransfers);
		if (remainingFee > 0 && purchaseAccount.SpotOrders.Any((SpotOrder item) => !item.IsFilled))
		{
			CancelOpenSpotOrders(purchaseAccount, spotMarket);
			remainingFee = PayHarvestFeeFromAvailableBalances(purchaseAccount, harvestAccount, remainingFee, closePrice, ref returnableSatoshi, satoshiTransfers);
		}
		if (remainingFee > 0)
		{
			List<ContractOrder> list = (from item in purchaseAccount.ContractOrders
				where !item.IsFilled
				orderby CalculateContractLiquidationDistanceRatio(item, closePrice), item.CreatedKLineIndex
				select item).ToList();
			foreach (ContractOrder item in list)
			{
				CancelOpenContractOrder(purchaseAccount, harvestAccount, item);
				contractMarket.Remove(item);
				remainingFee = PayHarvestFeeFromAvailableBalances(purchaseAccount, harvestAccount, remainingFee, closePrice, ref returnableSatoshi, satoshiTransfers);
				if (remainingFee == 0)
				{
					break;
				}
			}
		}
		if (remainingFee != 0)
		{
			return new HarvestFeePaymentResult(isPaid: false, 0L);
		}
		return new HarvestFeePaymentResult(isPaid: true, returnableSatoshi, satoshiTransfers);
	}

	private static long PayHarvestFeeFromAvailableBalances(TradingPointAccount purchaseAccount, TradingPointAccount harvestAccount, long remainingFee, decimal closePrice, ref long returnableSatoshi, List<SatoshiTransfer> satoshiTransfers)
	{
		if (remainingFee <= 0)
		{
			return 0L;
		}
		long val = Math.Max(purchaseAccount.SatoshiBalance, 0L);
		long num = Math.Min(val, remainingFee);
		purchaseAccount.SatoshiBalance -= num;
		harvestAccount.SatoshiBalance += num;
		if (num > 0)
		{
			satoshiTransfers.Add(new SatoshiTransfer(purchaseAccount, harvestAccount, num));
		}
		checked
		{
			returnableSatoshi += num;
		}
		remainingFee -= num;
		if (remainingFee > 0 && purchaseAccount.UBalance > 0m)
		{
			decimal num2 = ConvertSatoshiValueToU(remainingFee, closePrice);
			if (purchaseAccount.UBalance >= num2)
			{
				purchaseAccount.UBalance -= num2;
				harvestAccount.UBalance += num2;
				remainingFee = 0L;
			}
			else
			{
				decimal uBalance = purchaseAccount.UBalance;
				long num3 = Math.Min(remainingFee, FloorToSatoshi(ConvertUToSatoshiValue(uBalance, closePrice)));
				if (num3 > 0)
				{
					purchaseAccount.UBalance = 0m;
					harvestAccount.UBalance += uBalance;
					remainingFee -= num3;
				}
			}
		}
		return remainingFee;
	}

	private static decimal CalculateContractLiquidationDistanceRatio(ContractOrder contractOrder, decimal currentPrice)
	{
		if (currentPrice <= 0m)
		{
			throw new ArgumentOutOfRangeException("currentPrice");
		}
		return Math.Abs(currentPrice - contractOrder.LiquidationPrice) / currentPrice;
	}

	private static decimal CalculateHarvestPayableSatoshiValue(TradingPointAccount account, decimal closePrice)
	{
		decimal result = account.SatoshiBalance + FloorToSatoshi(ConvertUToSatoshiValue(account.UBalance, closePrice));
		foreach (SpotOrder item in account.SpotOrders.Where((SpotOrder item) => !item.IsFilled))
		{
			if (item.Side == SpotOrderSide.SellSatoshi)
			{
				result += (decimal)item.RemainingSatoshiAmount;
			}
			else
			{
				result += (decimal)FloorToSatoshi(ConvertUToSatoshiValue(item.RemainingUAmount, closePrice));
			}
		}
		foreach (ContractOrder item2 in account.ContractOrders.Where((ContractOrder item) => !item.IsFilled))
		{
			result += (decimal)FloorToSatoshi(GetRemainingContractMarginValueInSatoshi(item2, closePrice));
		}
		return result;
	}

	private static decimal CalculateAccountTotalAssetInSatoshi(TradingPointAccount account, decimal price)
	{
		if (account == null)
		{
			throw new ArgumentNullException("account");
		}
		if (price <= 0m)
		{
			throw new ArgumentOutOfRangeException("price");
		}
		decimal result = (decimal)account.SatoshiBalance + ConvertUToSatoshiValue(account.UBalance, price);
		foreach (SpotOrder item in account.SpotOrders.Where((SpotOrder item) => !item.IsFilled))
		{
			if (item.Side == SpotOrderSide.SellSatoshi)
			{
				result += (decimal)item.RemainingSatoshiAmount;
			}
			else
			{
				result += ConvertUToSatoshiValue(item.RemainingUAmount, price);
			}
		}
		return result;
	}

	private static long CalculateTrackedSatoshiTotal(IReadOnlyList<TradingPointAccount> tradingPointAccounts)
	{
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		long num = 0L;
		HashSet<Guid> hashSet = new HashSet<Guid>();
		checked
		{
			foreach (TradingPointAccount tradingPointAccount in tradingPointAccounts)
			{
				num += tradingPointAccount.SatoshiBalance;
				foreach (SpotOrder item in tradingPointAccount.SpotOrders.Where((SpotOrder item) => !item.IsFilled))
				{
					if (item.Side == SpotOrderSide.SellSatoshi && hashSet.Add(item.OrderId))
					{
						num += item.RemainingSatoshiAmount;
					}
				}
			}
			return num;
		}
	}

	private static void EnsureSatoshiTotalUnchanged(string operationName, long before, IReadOnlyList<TradingPointAccount> tradingPointAccounts)
	{
		long num = CalculateTrackedSatoshiTotal(tradingPointAccounts);
		if (num != before)
		{
			throw new InvalidOperationException($"Satoshi total changed during {operationName}. Before={before}, After={num}.");
		}
	}

	private static void CancelOpenSpotOrders(TradingPointAccount account, List<SpotOrder> spotMarket)
	{
		List<SpotOrder> openOrders = account.SpotOrders.Where((SpotOrder item) => !item.IsFilled).ToList();
		foreach (SpotOrder item in openOrders)
		{
			if (item.Side == SpotOrderSide.SellSatoshi)
			{
				account.SatoshiBalance += item.RemainingSatoshiAmount;
			}
			else
			{
				account.UBalance += item.RemainingUAmount;
			}
			item.Cancel();
		}
		spotMarket.RemoveAll((SpotOrder item) => openOrders.Contains(item));
	}

	private static void CancelOpenContractOrder(TradingPointAccount account, TradingPointAccount harvestAccount, ContractOrder contractOrder)
	{
		decimal num = CalculateRemainingContractMargin(contractOrder);
		if (contractOrder.MarginAsset == ContractMarginAsset.U)
		{
			harvestAccount.UBalance -= num;
			account.UBalance += num;
		}
		else
		{
			long num2 = FloorToSatoshi(num);
			harvestAccount.SatoshiBalance -= num2;
			account.SatoshiBalance += num2;
		}
		contractOrder.Cancel();
	}

	private static decimal CalculateRemainingContractMargin(ContractOrder contractOrder)
	{
		if (contractOrder.NominalPosition <= 0m)
		{
			return 0m;
		}
		decimal num = contractOrder.RemainingNominalPosition / contractOrder.NominalPosition;
		return contractOrder.MarginAmount * num;
	}

	private static decimal GetRemainingContractMarginValueInSatoshi(ContractOrder contractOrder, decimal price)
	{
		decimal num = CalculateRemainingContractMargin(contractOrder);
		return (contractOrder.MarginAsset == ContractMarginAsset.U) ? ConvertUToSatoshiValue(num, price) : num;
	}

	private static void LiquidateBankruptAccount(TradingPointAccount purchaseAccount, TradingPointAccount harvestAccount, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket, KLine? currentKLine)
	{
		purchaseAccount.IsBankrupt = true;
		harvestAccount.SatoshiBalance += purchaseAccount.SatoshiBalance;
		purchaseAccount.SatoshiBalance = 0L;
		harvestAccount.UBalance += purchaseAccount.UBalance;
		purchaseAccount.UBalance = 0m;
		foreach (SpotOrder item in purchaseAccount.SpotOrders.Where((SpotOrder item) => !item.IsFilled).ToList())
		{
			SettleBankruptSpotOrderToHarvest(item, harvestAccount);
		}
		spotMarket.RemoveAll((SpotOrder item) => item.Account == purchaseAccount);
		purchaseAccount.SpotOrders.Clear();
		List<ContractOrder> accountContracts = purchaseAccount.ContractOrders.ToList();
		foreach (ContractOrder item2 in accountContracts.Where((ContractOrder item) => !item.IsFilled))
		{
			SettleBankruptContractOrderToHarvest(item2, harvestAccount, currentKLine);
		}
		contractMarket.RemoveAll((ContractOrder item) => accountContracts.Contains(item));
		purchaseAccount.ContractOrders.Clear();
	}

	private static void SettleBankruptSpotOrderToHarvest(SpotOrder spotOrder, TradingPointAccount harvestAccount)
	{
		if (spotOrder.Side == SpotOrderSide.SellSatoshi)
		{
			harvestAccount.SatoshiBalance += spotOrder.RemainingSatoshiAmount;
		}
		else
		{
			harvestAccount.UBalance += spotOrder.RemainingUAmount;
		}
		spotOrder.Cancel();
	}

	private static void SettleBankruptContractOrderToHarvest(ContractOrder contractOrder, TradingPointAccount harvestAccount, KLine? currentKLine)
	{
		if (!contractOrder.IsFilled)
		{
			ContractKLineResult contractKLineResult = ((currentKLine != null) ? GetContractKLineResult(contractOrder, currentKLine) : ContractKLineResult.None);
			if (contractKLineResult == ContractKLineResult.TakeProfit)
			{
				SettleContractTakeProfitToHarvest(contractOrder, harvestAccount, contractOrder.RemainingNominalPosition);
				return;
			}
			TransferRemainingContractMarginToHarvest(contractOrder, harvestAccount);
			contractOrder.Cancel();
		}
	}

	private static void MatchingTrade(int kLineIndex, KLine currentKLine, FiveElement currentFiveElement, List<TradingPointAccount> tradingPointAccounts, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket)
	{
		if (currentKLine == null)
		{
			throw new ArgumentNullException("currentKLine");
		}
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (spotMarket == null)
		{
			throw new ArgumentNullException("spotMarket");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		TradingPointAccount harvestAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		List<TradingPointAccount> purchaseAccounts = tradingPointAccounts.Where((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Purchase && !item.IsBankrupt).ToList();
		Dictionary<int, TradingPointAccount> circleAccounts = BuildCircleAccountMap(purchaseAccounts);
		List<RouteContractOrderResult> list = new List<RouteContractOrderResult>();
		List<SpotTrade> list2 = MatchMatureSpotOrders(kLineIndex, currentKLine, currentFiveElement, harvestAccount, circleAccounts, spotMarket, contractMarket, list);
		int value = MatchMatureContractOrders(kLineIndex, currentKLine, currentFiveElement, harvestAccount, circleAccounts, contractMarket, list);
		spotMarket.RemoveAll((SpotOrder item) => item.IsFilled);
		contractMarket.RemoveAll((ContractOrder item) => item.IsFilled);
		Console.WriteLine($"Matching trade: spotTrades={list2.Count}, contractTrades={value}, routeContracts={list.Sum((RouteContractOrderResult item) => item.OrderCount)}, activeSpotOrders={spotMarket.Count}, activeContractOrders={contractMarket.Count}.");
	}

	private static SpotMarketResult SpotMarket(int kLineIndex, KLine currentKLine, FiveElement currentFiveElement, List<TradingPointAccount> tradingPointAccounts, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket)
	{
		if (currentKLine == null)
		{
			throw new ArgumentNullException("currentKLine");
		}
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (spotMarket == null)
		{
			throw new ArgumentNullException("spotMarket");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		List<TradingPointAccount> purchaseAccounts = tradingPointAccounts.Where((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Purchase && !item.IsBankrupt).ToList();
		TradingPointAccount tradingPointAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		List<RouteContractOrderResult> routeContractOrderResults = new List<RouteContractOrderResult>();
		List<SpotTrade> trades = new List<SpotTrade>();
		List<SpotOrder> list = CreateSpotOrdersFromAssetDeviation(kLineIndex, currentKLine, currentFiveElement, purchaseAccounts, spotMarket);
		SpotOrder? spotOrder = TryCreateHarvestSpotOrderFromBalance(tradingPointAccount, kLineIndex, currentKLine, currentFiveElement);
		if (spotOrder != null)
		{
			tradingPointAccount.SpotOrders.Add(spotOrder);
			spotMarket.Add(spotOrder);
			list.Add(spotOrder);
		}
		Console.WriteLine($"Spot market: createdOrders={list.Count}, activeOrders={spotMarket.Count}.");
		return new SpotMarketResult(trades, list, routeContractOrderResults);
	}

	private static List<SpotTrade> MatchMatureSpotOrders(int kLineIndex, KLine currentKLine, FiveElement currentFiveElement, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		List<SpotTrade> list = new List<SpotTrade>();
		List<SpotOrder> list2 = (from item in spotMarket
			where item.Side == SpotOrderSide.SellSatoshi && !item.IsFilled && item.CanTradeAt(kLineIndex) && IsPriceInsideKLine(item.Price, currentKLine)
			orderby item.DeviationPercent descending
			select item).ToList();
		List<SpotOrder> source = (from item in spotMarket
			where item.Side == SpotOrderSide.BuySatoshi && !item.IsFilled && item.CanTradeAt(kLineIndex) && IsPriceInsideKLine(item.Price, currentKLine)
			orderby item.DeviationPercent descending
			select item).ToList();
		foreach (SpotOrder sellOrder in list2)
		{
			while (!sellOrder.IsFilled)
			{
				SpotOrder? spotOrder = source.FirstOrDefault((SpotOrder item) => !item.IsFilled && item.Account != sellOrder.Account && item.Price >= sellOrder.Price);
				if (spotOrder == null)
				{
					break;
				}
				SpotTrade? spotTrade = TryExecuteSpotTrade(sellOrder, spotOrder, harvestAccount, spotOrder.Price, kLineIndex, currentFiveElement, circleAccounts, contractMarket, routeContractOrderResults);
				if (spotTrade != null)
				{
					list.Add(spotTrade);
					continue;
				}
				break;
			}
		}
		foreach (SpotOrder item in list2.Where((SpotOrder item) => !item.IsFilled && item.Account != harvestAccount))
		{
			SpotTrade? spotTrade2 = TryExecuteSpotTradeWithHarvestBuyer(item, harvestAccount, kLineIndex, currentFiveElement, circleAccounts, contractMarket, routeContractOrderResults);
			if (spotTrade2 != null)
			{
				list.Add(spotTrade2);
			}
		}
		foreach (SpotOrder item2 in source.Where((SpotOrder item) => !item.IsFilled && item.Account != harvestAccount))
		{
			SpotTrade? spotTrade3 = TryExecuteSpotTradeWithHarvestSeller(item2, harvestAccount, kLineIndex, currentFiveElement, circleAccounts, contractMarket, routeContractOrderResults);
			if (spotTrade3 != null)
			{
				list.Add(spotTrade3);
			}
		}
		return list;
	}

	private static int MatchMatureContractOrders(int kLineIndex, KLine currentKLine, FiveElement currentFiveElement, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		int num = 0;
		int num2 = SettleLiquidatedContractOrders(kLineIndex, currentKLine, currentFiveElement, harvestAccount, circleAccounts, contractMarket, routeContractOrderResults);
		List<ContractOrder> list = (from item in contractMarket
			where item.Direction == ContractDirection.Long && !item.IsFilled && item.CanTradeAt(kLineIndex) && GetContractKLineResult(item, currentKLine) == ContractKLineResult.TakeProfit
			orderby item.TakeProfitPrice descending, item.CreatedKLineIndex
			select item).ToList();
		List<ContractOrder> source = (from item in contractMarket
			where item.Direction == ContractDirection.Short && !item.IsFilled && item.CanTradeAt(kLineIndex) && GetContractKLineResult(item, currentKLine) == ContractKLineResult.TakeProfit
			orderby item.TakeProfitPrice, item.CreatedKLineIndex
			select item).ToList();
		foreach (ContractOrder longOrder in list)
		{
			while (!longOrder.IsFilled)
			{
				ContractOrder? contractOrder = source.FirstOrDefault((ContractOrder item) => !item.IsFilled && !IsSameContractOwner(item, longOrder) && longOrder.TakeProfitPrice >= item.TakeProfitPrice);
				if (contractOrder == null || !TryExecuteContractTrade(longOrder, contractOrder, longOrder.TakeProfitPrice, kLineIndex, currentFiveElement, harvestAccount, circleAccounts, contractMarket, routeContractOrderResults))
				{
					break;
				}
				num++;
			}
		}
		foreach (ContractOrder item in list.Where((ContractOrder item) => !item.IsFilled))
		{
			if (TryExecuteContractTradeWithHarvest(item, harvestAccount, kLineIndex, currentFiveElement, circleAccounts, contractMarket, routeContractOrderResults))
			{
				num++;
			}
		}
		foreach (ContractOrder item2 in source.Where((ContractOrder item) => !item.IsFilled))
		{
			if (TryExecuteContractTradeWithHarvest(item2, harvestAccount, kLineIndex, currentFiveElement, circleAccounts, contractMarket, routeContractOrderResults))
			{
				num++;
			}
		}
		if (num2 > 0)
		{
			Console.WriteLine($"Contract liquidation done: count={num2}.");
		}
		return num;
	}

	private static int SettleLiquidatedContractOrders(int kLineIndex, KLine currentKLine, FiveElement currentFiveElement, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		int num = 0;
		foreach (ContractOrder item in contractMarket.Where((ContractOrder item) => !item.IsFilled && item.CanTradeAt(kLineIndex) && GetContractKLineResult(item, currentKLine) == ContractKLineResult.Liquidation).ToList())
		{
			TransferRemainingContractMarginToHarvest(item, harvestAccount);
			item.Fill(item.RemainingNominalPosition);
			num++;
		}
		return num;
	}

	private static ContractKLineResult GetContractKLineResult(ContractOrder order, KLine currentKLine)
	{
		bool flag = IsContractTakeProfitTouched(order, currentKLine);
		bool flag2 = IsContractLiquidationTouched(order, currentKLine);
		if (!flag && !flag2)
		{
			return ContractKLineResult.None;
		}
		if (flag && !flag2)
		{
			return ContractKLineResult.TakeProfit;
		}
		if (!flag)
		{
			return ContractKLineResult.Liquidation;
		}
		return ShouldPreferTakeProfitWhenBothTouched(order, currentKLine) ? ContractKLineResult.TakeProfit : ContractKLineResult.Liquidation;
	}

	private static bool IsContractTakeProfitTouched(ContractOrder order, KLine currentKLine)
	{
		return (order.Direction == ContractDirection.Long) ? (currentKLine.HighValue >= order.TakeProfitPrice) : (currentKLine.LowValue <= order.TakeProfitPrice);
	}

	private static bool IsContractLiquidationTouched(ContractOrder order, KLine currentKLine)
	{
		return (order.Direction == ContractDirection.Long) ? (currentKLine.LowValue <= order.LiquidationPrice) : (currentKLine.HighValue >= order.LiquidationPrice);
	}

	private static bool ShouldPreferTakeProfitWhenBothTouched(ContractOrder order, KLine currentKLine)
	{
		if (order.Direction == ContractDirection.Long)
		{
			return currentKLine.CloseValue > currentKLine.OpenValue;
		}
		return currentKLine.CloseValue < currentKLine.OpenValue;
	}

	private static bool TryExecuteContractTrade(ContractOrder longOrder, ContractOrder shortOrder, decimal tradePrice, int kLineIndex, FiveElement currentFiveElement, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		if (longOrder.Direction != 0)
		{
			throw new ArgumentException("Long order direction is invalid.", "longOrder");
		}
		if (shortOrder.Direction != ContractDirection.Short)
		{
			throw new ArgumentException("Short order direction is invalid.", "shortOrder");
		}
		decimal remainingContractValueInSatoshi = GetRemainingContractValueInSatoshi(longOrder, tradePrice);
		decimal remainingContractValueInSatoshi2 = GetRemainingContractValueInSatoshi(shortOrder, tradePrice);
		decimal num = Math.Min(remainingContractValueInSatoshi, remainingContractValueInSatoshi2);
		if (num <= 0m)
		{
			return false;
		}
		TradingPointAccount contractOwnerAccount = GetContractOwnerAccount(longOrder, circleAccounts);
		TradingPointAccount contractOwnerAccount2 = GetContractOwnerAccount(shortOrder, circleAccounts);
		decimal num2 = CalculateContractNominalPositionBySatoshiValue(longOrder, num, tradePrice);
		decimal num3 = CalculateContractNominalPositionBySatoshiValue(shortOrder, num, tradePrice);
		if (num2 <= 0m || num3 <= 0m)
		{
			return false;
		}
		EnsureHarvestCanPayContractTakeProfit(
			harvestAccount,
			(longOrder, num2),
			(shortOrder, num3));
		SatoshiTransfer? satoshiPayout = SettleContractTakeProfit(longOrder, contractOwnerAccount, harvestAccount, num2);
		SatoshiTransfer? satoshiPayout2 = SettleContractTakeProfit(shortOrder, contractOwnerAccount2, harvestAccount, num3);
		RouteContractOrderResult? routeContractOrderResult = TryAddContractOrdersForMatchingTrade(contractOwnerAccount, contractOwnerAccount2, kLineIndex, currentFiveElement, tradePrice, CalculatePathEffectMultiplier(longOrder.FiveElement, shortOrder.FiveElement), harvestAccount, circleAccounts, contractMarket);
		if (routeContractOrderResult != null)
		{
			routeContractOrderResults.Add(routeContractOrderResult);
		}
		AddSatoshiPayoutPathEffectIfNeeded(satoshiPayout, kLineIndex, currentFiveElement, tradePrice, harvestAccount, circleAccounts, contractMarket, routeContractOrderResults);
		AddSatoshiPayoutPathEffectIfNeeded(satoshiPayout2, kLineIndex, currentFiveElement, tradePrice, harvestAccount, circleAccounts, contractMarket, routeContractOrderResults);
		return true;
	}

	private static bool TryExecuteContractTradeWithHarvest(ContractOrder order, TradingPointAccount harvestAccount, int kLineIndex, FiveElement currentFiveElement, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		if (order.IsFilled)
		{
			return false;
		}
		decimal remainingNominalPosition = order.RemainingNominalPosition;
		if (remainingNominalPosition <= 0m)
		{
			return false;
		}
		TradingPointAccount contractOwnerAccount = GetContractOwnerAccount(order, circleAccounts);
		SatoshiTransfer? satoshiPayout = SettleContractTakeProfit(order, contractOwnerAccount, harvestAccount, remainingNominalPosition);
		RouteContractOrderResult? routeContractOrderResult = TryAddContractOrdersForMatchingTrade(contractOwnerAccount, harvestAccount, kLineIndex, currentFiveElement, order.TakeProfitPrice, CalculatePathEffectMultiplier(order.FiveElement, currentFiveElement), harvestAccount, circleAccounts, contractMarket);
		if (routeContractOrderResult != null)
		{
			routeContractOrderResults.Add(routeContractOrderResult);
		}
		AddSatoshiPayoutPathEffectIfNeeded(satoshiPayout, kLineIndex, currentFiveElement, order.TakeProfitPrice, harvestAccount, circleAccounts, contractMarket, routeContractOrderResults);
		return true;
	}

	private static void EnsureHarvestCanPayContractTakeProfit(
		TradingPointAccount harvestAccount,
		params (ContractOrder Order, decimal NominalPosition)[] settlements)
	{
		if (harvestAccount == null)
		{
			throw new ArgumentNullException("harvestAccount");
		}
		if (settlements == null)
		{
			throw new ArgumentNullException("settlements");
		}
		long requiredSatoshi = 0L;
		foreach ((ContractOrder Order, decimal NominalPosition) settlement in settlements)
		{
			if (settlement.Order.MarginAsset != ContractMarginAsset.Satoshi)
			{
				continue;
			}
			var (_, _, payout) = CalculateContractTakeProfitSettlement(
				settlement.Order,
				settlement.NominalPosition);
			requiredSatoshi = checked(requiredSatoshi + FloorToSatoshi(payout));
		}
		if (harvestAccount.SatoshiBalance < requiredSatoshi)
		{
			throw new InvalidOperationException(
				$"Harvest account does not have enough satoshi to settle contract take profit. Required={requiredSatoshi}, Available={harvestAccount.SatoshiBalance}.");
		}
	}

	private static TradingPointAccount GetContractOwnerAccount(ContractOrder order, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts)
	{
		if (circleAccounts.TryGetValue(order.OwnerCircleIndex, out TradingPointAccount? value) &&
			string.Equals(value.TradingPoint.OwnerCircleId, order.OwnerCircleId, StringComparison.Ordinal))
		{
			return value;
		}
		TradingPointAccount? accountById = circleAccounts.Values.SingleOrDefault(
			(TradingPointAccount item) => string.Equals(item.TradingPoint.OwnerCircleId, order.OwnerCircleId, StringComparison.Ordinal));
		if (accountById == null)
		{
			throw new InvalidOperationException($"Contract owner circle {order.OwnerCircleIndex} does not have an account.");
		}
		return accountById;
	}

	private static bool IsSameContractOwner(ContractOrder left, ContractOrder right)
	{
		if (left == null)
		{
			throw new ArgumentNullException("left");
		}
		if (right == null)
		{
			throw new ArgumentNullException("right");
		}
		return string.Equals(left.OwnerCircleId, right.OwnerCircleId, StringComparison.Ordinal);
	}

	private static decimal GetRemainingContractValueInSatoshi(ContractOrder order, decimal price)
	{
		return (order.MarginAsset == ContractMarginAsset.U) ? ConvertUToSatoshiValue(order.RemainingNominalPosition, price) : order.RemainingNominalPosition;
	}

	private static decimal CalculateContractNominalPositionBySatoshiValue(ContractOrder order, decimal satoshiValue, decimal price)
	{
		decimal val = ((order.MarginAsset == ContractMarginAsset.U) ? ConvertSatoshiValueToU(satoshiValue, price) : ((decimal)FloorToSatoshi(satoshiValue)));
		val = Math.Min(val, order.RemainingNominalPosition);
		if (val <= 0m)
		{
			return 0m;
		}
		return val;
	}

	private static SatoshiTransfer? SettleContractTakeProfit(ContractOrder order, TradingPointAccount account, TradingPointAccount harvestAccount, decimal nominalPosition)
	{
		var (num, num2, num3) = CalculateContractTakeProfitSettlement(order, nominalPosition);
		if (order.MarginAsset == ContractMarginAsset.U)
		{
			harvestAccount.UBalance -= num3;
			account.UBalance += num3;
			order.Fill(nominalPosition);
			return null;
		}
		long num4 = FloorToSatoshi(num3);
		if (harvestAccount.SatoshiBalance < num4)
		{
			throw new InvalidOperationException($"Harvest account does not have enough satoshi to settle contract take profit. Required={num4}, Available={harvestAccount.SatoshiBalance}.");
		}
		harvestAccount.SatoshiBalance -= num4;
		account.SatoshiBalance += num4;
		order.Fill(nominalPosition);
		return (num4 > 0) ? new SatoshiTransfer?(new SatoshiTransfer(harvestAccount, account, num4)) : ((SatoshiTransfer?)null);
	}

	private static void SettleContractTakeProfitToHarvest(ContractOrder order, TradingPointAccount harvestAccount, decimal nominalPosition)
	{
		CalculateContractTakeProfitSettlement(order, nominalPosition);
		order.Fill(nominalPosition);
	}

	private static (decimal MarginReturn, decimal Profit, decimal Payout) CalculateContractTakeProfitSettlement(ContractOrder order, decimal nominalPosition)
	{
		if (nominalPosition <= 0m)
		{
			throw new ArgumentOutOfRangeException("nominalPosition");
		}
		if (nominalPosition > order.RemainingNominalPosition)
		{
			throw new InvalidOperationException("Contract settlement nominal position exceeds remaining position.");
		}
		decimal num = order.MarginAmount * nominalPosition / order.NominalPosition;
		decimal num2 = ((order.Direction == ContractDirection.Long) ? ((order.TakeProfitPrice - order.Price) / order.Price) : ((order.Price - order.TakeProfitPrice) / order.Price));
		decimal num3 = nominalPosition * num2;
		decimal item = num + num3;
		return (MarginReturn: num, Profit: num3, Payout: item);
	}

	private static void TransferRemainingContractMarginToHarvest(ContractOrder order, TradingPointAccount harvestAccount)
	{
		decimal num = CalculateRemainingContractMargin(order);
		if (!(num <= 0m) && order.MarginAsset != ContractMarginAsset.U)
		{
		}
	}

	private static RouteContractOrderResult? TryAddContractOrdersForMatchingTrade(TradingPointAccount sourceAccount, TradingPointAccount targetAccount, int kLineIndex, FiveElement currentFiveElement, decimal price, decimal pathEffectMultiplier, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket)
	{
		if (sourceAccount == targetAccount)
		{
			return null;
		}
		IReadOnlyList<KChartRouteInfo> readOnlyList = KChartRunWithFiveElementsAPI.LoadRoutesFromTradingPoint(sourceAccount.TradingPoint, new KChartTradingPointInfo[1] { targetAccount.TradingPoint });
		if (readOnlyList.Count != 1)
		{
			throw new InvalidOperationException($"Matching trade route count {readOnlyList.Count} is invalid.");
		}
		return AddContractOrdersFromRoute(sourceAccount, targetAccount, readOnlyList[0], circleAccounts, harvestAccount, kLineIndex, currentFiveElement, price, pathEffectMultiplier, contractMarket);
	}

	private static IReadOnlyList<RouteContractOrderResult> AddContractOrdersForSatoshiTransfers(IReadOnlyList<SatoshiTransfer> satoshiTransfers, int kLineIndex, FiveElement currentFiveElement, decimal price, IReadOnlyList<TradingPointAccount> tradingPointAccounts, List<ContractOrder> contractMarket)
	{
		if (satoshiTransfers == null)
		{
			throw new ArgumentNullException("satoshiTransfers");
		}
		if (price <= 0m)
		{
			throw new ArgumentOutOfRangeException("price");
		}
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		if (satoshiTransfers.Count == 0)
		{
			return Array.Empty<RouteContractOrderResult>();
		}
		TradingPointAccount harvestAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		Dictionary<int, TradingPointAccount> circleAccounts = BuildCircleAccountMap(tradingPointAccounts.Where((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Purchase && !item.IsBankrupt).ToList());
		List<RouteContractOrderResult> list = new List<RouteContractOrderResult>(satoshiTransfers.Count);
		foreach (SatoshiTransfer satoshiTransfer in satoshiTransfers)
		{
			RouteContractOrderResult? routeContractOrderResult = TryAddContractOrdersForMatchingTrade(satoshiTransfer.SourceAccount, satoshiTransfer.TargetAccount, kLineIndex, currentFiveElement, price, 1m, harvestAccount, circleAccounts, contractMarket);
			if (routeContractOrderResult != null)
			{
				list.Add(routeContractOrderResult);
			}
		}
		if (list.Count > 0)
		{
			Console.WriteLine($"Satoshi transfer path effects: transfers={satoshiTransfers.Count}, routeContracts={list.Sum((RouteContractOrderResult item) => item.OrderCount)}.");
		}
		return list;
	}

	private static void AddSatoshiPayoutPathEffectIfNeeded(SatoshiTransfer? satoshiPayout, int kLineIndex, FiveElement currentFiveElement, decimal price, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		if (satoshiPayout.HasValue)
		{
			SatoshiTransfer value = satoshiPayout.Value;
			RouteContractOrderResult? routeContractOrderResult = TryAddContractOrdersForMatchingTrade(value.SourceAccount, value.TargetAccount, kLineIndex, currentFiveElement, price, 1m, harvestAccount, circleAccounts, contractMarket);
			if (routeContractOrderResult != null)
			{
				routeContractOrderResults.Add(routeContractOrderResult);
			}
		}
	}

	private static List<SpotOrder> CreateSpotOrdersFromAssetDeviation(int kLineIndex, KLine currentKLine, FiveElement currentFiveElement, IReadOnlyList<TradingPointAccount> purchaseAccounts, List<SpotOrder> spotMarket)
	{
		List<TradingPointAccount> list = purchaseAccounts.OrderByDescending((TradingPointAccount item) => CalculateAssetDeviationPercent(item, currentKLine.OpenValue)).ToList();
		List<SpotOrder> list2 = new List<SpotOrder>();
		foreach (TradingPointAccount item in list)
		{
			SpotOrder? spotOrder = TryCreateSpotOrderFromAssetDeviation(item, kLineIndex, currentKLine, currentFiveElement);
			if (spotOrder != null)
			{
				item.SpotOrders.Add(spotOrder);
				spotMarket.Add(spotOrder);
				list2.Add(spotOrder);
			}
		}
		return list2;
	}

	private static SpotOrder? TryCreateSpotOrderFromAssetDeviation(TradingPointAccount account, int kLineIndex, KLine currentKLine, FiveElement currentFiveElement)
	{
		decimal openValue = currentKLine.OpenValue;
		decimal num = ConvertUToSatoshiValue(account.UBalance, openValue);
		decimal num2 = (decimal)account.SatoshiBalance + num;
		if (num2 <= 0m)
		{
			return null;
		}
		decimal num3 = CalculateTargetBtcRatio(account.TradingPoint.OwnerCircleId);
		decimal num4 = (decimal)account.SatoshiBalance / num2;
		decimal num5 = num4 - num3;
		decimal price = CreateRandomSpotOrderPrice(currentKLine);
		if (num5 > 0m)
		{
			decimal num6 = (decimal)account.SatoshiBalance - num2 * num3;
			long num7 = FloorToSatoshi(num6 * 0.20m);
			if (num7 <= 0)
			{
				return null;
			}
			account.SatoshiBalance -= num7;
			return new SpotOrder(account, SpotOrderSide.SellSatoshi, num7, ConvertSatoshiToU(num7, price), Math.Abs(num5), kLineIndex, kLineIndex + 1, currentFiveElement, price);
		}
		if (num5 < 0m)
		{
			decimal num8 = num2 * num3 - (decimal)account.SatoshiBalance;
			long val = FloorToSatoshi(num8 * 0.20m);
			long val2 = FloorToSatoshi(ConvertUToSatoshiValue(account.UBalance, price));
			val = Math.Min(val, val2);
			if (val <= 0)
			{
				return null;
			}
			decimal num9 = ConvertSatoshiToU(val, price);
			if (num9 <= 0m || num9 > account.UBalance)
			{
				return null;
			}
			account.UBalance -= num9;
			return new SpotOrder(account, SpotOrderSide.BuySatoshi, val, num9, Math.Abs(num5), kLineIndex, kLineIndex + 1, currentFiveElement, price);
		}
		return null;
	}

	private static SpotOrder? TryCreateHarvestSpotOrderFromBalance(TradingPointAccount harvestAccount, int kLineIndex, KLine currentKLine, FiveElement currentFiveElement)
	{
		decimal openValue = currentKLine.OpenValue;
		decimal num = ConvertUToSatoshiValue(harvestAccount.UBalance, openValue);
		decimal num2 = (decimal)harvestAccount.SatoshiBalance + num;
		if (num2 <= 0m)
		{
			return null;
		}
		decimal num3 = 0.5m;
		decimal num4 = (decimal)harvestAccount.SatoshiBalance / num2;
		decimal num5 = num4 - num3;
		decimal price = CreateRandomSpotOrderPrice(currentKLine);
		if (num5 > 0m)
		{
			decimal num6 = (decimal)harvestAccount.SatoshiBalance - num2 * num3;
			long num7 = FloorToSatoshi(num6 * 0.20m);
			if (num7 <= 0)
			{
				return null;
			}
			harvestAccount.SatoshiBalance -= num7;
			return new SpotOrder(harvestAccount, SpotOrderSide.SellSatoshi, num7, ConvertSatoshiToU(num7, price), Math.Abs(num5), kLineIndex, kLineIndex + 1, currentFiveElement, price);
		}
		if (num5 < 0m)
		{
			decimal num8 = num2 * num3 - (decimal)harvestAccount.SatoshiBalance;
			long val = FloorToSatoshi(num8 * 0.20m);
			long val2 = FloorToSatoshi(ConvertUToSatoshiValue(harvestAccount.UBalance, price));
			val = Math.Min(val, val2);
			if (val <= 0)
			{
				return null;
			}
			decimal num9 = ConvertSatoshiToU(val, price);
			if (num9 <= 0m || num9 > harvestAccount.UBalance)
			{
				return null;
			}
			harvestAccount.UBalance -= num9;
			return new SpotOrder(harvestAccount, SpotOrderSide.BuySatoshi, val, num9, Math.Abs(num5), kLineIndex, kLineIndex + 1, currentFiveElement, price);
		}
		return null;
	}

	private static SpotTrade? TryExecuteSpotTrade(SpotOrder sellOrder, SpotOrder buyOrder, TradingPointAccount harvestAccount, decimal tradePrice, int kLineIndex, FiveElement currentFiveElement, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		if (sellOrder.Side != SpotOrderSide.SellSatoshi)
		{
			throw new ArgumentException("Sell order side is invalid.", "sellOrder");
		}
		if (buyOrder.Side != 0)
		{
			throw new ArgumentException("Buy order side is invalid.", "buyOrder");
		}
		if (tradePrice <= 0m)
		{
			throw new ArgumentOutOfRangeException("tradePrice");
		}
		long val = Math.Min(sellOrder.RemainingSatoshiAmount, buyOrder.RemainingSatoshiAmount);
		long val2 = FloorToSatoshi(ConvertUToSatoshiValue(buyOrder.RemainingUAmount, tradePrice));
		val = Math.Min(val, val2);
		if (val <= 0)
		{
			return null;
		}
		decimal num = ConvertSatoshiToU(val, tradePrice);
		if (num <= 0m)
		{
			return null;
		}
		sellOrder.Account.UBalance += num;
		buyOrder.Account.SatoshiBalance += val;
		sellOrder.Fill(val, num);
		buyOrder.Fill(val, num);
		RouteContractOrderResult routeContractOrderResult = AddContractOrdersForSpotTrade(sellOrder.Account, buyOrder.Account, kLineIndex, currentFiveElement, tradePrice, CalculatePathEffectMultiplier(sellOrder.FiveElement, buyOrder.FiveElement), harvestAccount, circleAccounts, contractMarket);
		routeContractOrderResults.Add(routeContractOrderResult);
		return new SpotTrade(sellOrder.Account, buyOrder.Account, val, num, tradePrice, kLineIndex, currentFiveElement, routeContractOrderResult);
	}

	private static SpotTrade? TryExecuteSpotTradeWithHarvestBuyer(SpotOrder sellOrder, TradingPointAccount harvestAccount, int kLineIndex, FiveElement currentFiveElement, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		if (sellOrder.Side != SpotOrderSide.SellSatoshi)
		{
			throw new ArgumentException("Sell order side is invalid.", "sellOrder");
		}
		long remainingSatoshiAmount = sellOrder.RemainingSatoshiAmount;
		if (remainingSatoshiAmount <= 0)
		{
			return null;
		}
		decimal price = sellOrder.Price;
		decimal num = ConvertSatoshiToU(remainingSatoshiAmount, price);
		if (num <= 0m)
		{
			return null;
		}
		sellOrder.Account.UBalance += num;
		harvestAccount.UBalance -= num;
		harvestAccount.SatoshiBalance += remainingSatoshiAmount;
		sellOrder.Fill(remainingSatoshiAmount, num);
		RouteContractOrderResult routeContractOrderResult = AddContractOrdersForSpotTrade(sellOrder.Account, harvestAccount, kLineIndex, currentFiveElement, price, CalculatePathEffectMultiplier(sellOrder.FiveElement, currentFiveElement), harvestAccount, circleAccounts, contractMarket);
		routeContractOrderResults.Add(routeContractOrderResult);
		return new SpotTrade(sellOrder.Account, harvestAccount, remainingSatoshiAmount, num, price, kLineIndex, currentFiveElement, routeContractOrderResult);
	}

	private static SpotTrade? TryExecuteSpotTradeWithHarvestSeller(SpotOrder buyOrder, TradingPointAccount harvestAccount, int kLineIndex, FiveElement currentFiveElement, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket, List<RouteContractOrderResult> routeContractOrderResults)
	{
		if (buyOrder.Side != 0)
		{
			throw new ArgumentException("Buy order side is invalid.", "buyOrder");
		}
		long remainingSatoshiAmount = buyOrder.RemainingSatoshiAmount;
		long val = FloorToSatoshi(ConvertUToSatoshiValue(buyOrder.RemainingUAmount, buyOrder.Price));
		remainingSatoshiAmount = Math.Min(remainingSatoshiAmount, val);
		remainingSatoshiAmount = Math.Min(remainingSatoshiAmount, harvestAccount.SatoshiBalance);
		if (remainingSatoshiAmount <= 0)
		{
			return null;
		}
		decimal price = buyOrder.Price;
		decimal num = ConvertSatoshiToU(remainingSatoshiAmount, price);
		if (num <= 0m)
		{
			return null;
		}
		harvestAccount.SatoshiBalance -= remainingSatoshiAmount;
		harvestAccount.UBalance += num;
		buyOrder.Account.SatoshiBalance += remainingSatoshiAmount;
		buyOrder.Fill(remainingSatoshiAmount, num);
		RouteContractOrderResult routeContractOrderResult = AddContractOrdersForSpotTrade(harvestAccount, buyOrder.Account, kLineIndex, currentFiveElement, price, CalculatePathEffectMultiplier(buyOrder.FiveElement, currentFiveElement), harvestAccount, circleAccounts, contractMarket);
		routeContractOrderResults.Add(routeContractOrderResult);
		return new SpotTrade(harvestAccount, buyOrder.Account, remainingSatoshiAmount, num, price, kLineIndex, currentFiveElement, routeContractOrderResult);
	}

	private static RouteContractOrderResult AddContractOrdersForSpotTrade(TradingPointAccount sourceAccount, TradingPointAccount targetAccount, int kLineIndex, FiveElement currentFiveElement, decimal price, decimal pathEffectMultiplier, TradingPointAccount harvestAccount, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, List<ContractOrder> contractMarket)
	{
		IReadOnlyList<KChartRouteInfo> readOnlyList = KChartRunWithFiveElementsAPI.LoadRoutesFromTradingPoint(sourceAccount.TradingPoint, new KChartTradingPointInfo[1] { targetAccount.TradingPoint });
		if (readOnlyList.Count != 1)
		{
			throw new InvalidOperationException($"Spot trade route count {readOnlyList.Count} is invalid.");
		}
		return AddContractOrdersFromRoute(sourceAccount, targetAccount, readOnlyList[0], circleAccounts, harvestAccount, kLineIndex, currentFiveElement, price, pathEffectMultiplier, contractMarket);
	}

	private static bool IsPriceInsideKLine(decimal price, KLine currentKLine)
	{
		return price >= currentKLine.LowValue && price <= currentKLine.HighValue;
	}

	private static decimal CalculateAssetDeviationPercent(TradingPointAccount account, decimal price)
	{
		if (price <= 0m)
		{
			throw new ArgumentOutOfRangeException("price");
		}
		decimal num = ConvertUToSatoshiValue(account.UBalance, price);
		decimal num2 = (decimal)account.SatoshiBalance + num;
		if (num2 <= 0m)
		{
			return 0m;
		}
		decimal num3 = CalculateTargetBtcRatio(account.TradingPoint.OwnerCircleId);
		decimal num4 = (decimal)account.SatoshiBalance / num2;
		return Math.Abs(num4 - num3);
	}

	private static decimal CalculateTargetBtcRatio(string ownerCircleId)
	{
		ValidateCircleId(ownerCircleId, "ownerCircleId");
		byte[] source = Convert.FromHexString(ownerCircleId);
		byte[] array = SHA256.HashData(source);
		int num = array[0] % 80;
		return (10m + (decimal)num) / 100m;
	}

	private static decimal CalculatePathEffectMultiplier(FiveElement left, FiveElement right)
	{
		FiveElementRelation relation = FiveElementRelationCalculator.GetRelation(left, right);
		if (1 == 0)
		{
		}
		decimal result = relation switch
		{
			FiveElementRelation.Generating => 2m, 
			FiveElementRelation.Restraining => 0.5m, 
			_ => 1m, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static void ValidateCircleId(string circleId, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(circleId))
		{
			throw new ArgumentException("Circle id cannot be null or empty.", parameterName);
		}
		if (circleId.Length != 24)
		{
			throw new ArgumentException("Circle id must be 24 lowercase hexadecimal characters.", parameterName);
		}
		foreach (char c in circleId)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
			{
				throw new ArgumentException("Circle id must be 24 lowercase hexadecimal characters.", parameterName);
			}
		}
	}

	private static decimal CreateRandomSpotOrderPrice(KLine currentKLine)
	{
		if (currentKLine.HighValue == currentKLine.LowValue)
		{
			return currentKLine.LowValue;
		}
		decimal num = (decimal)Random.Shared.NextDouble();
		return currentKLine.LowValue + (currentKLine.HighValue - currentKLine.LowValue) * num;
	}

	private static void ExecuteDistribution(int kLineIndex, decimal price, FiveElement currentFiveElement, List<TradingPointAccount> tradingPointAccounts, List<ContractOrder> contractMarket, long reservedHarvestSatoshi)
	{
		if (price <= 0m)
		{
			throw new ArgumentOutOfRangeException("price", "Price must be greater than 0.");
		}
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		if (reservedHarvestSatoshi < 0)
		{
			throw new ArgumentOutOfRangeException("reservedHarvestSatoshi");
		}
		TradingPointAccount tradingPointAccount = tradingPointAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		List<TradingPointAccount> list = tradingPointAccounts.Where((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Purchase && !item.IsBankrupt).ToList();
		if (list.Count == 0)
		{
			throw new InvalidOperationException("Purchase account count must be greater than 0.");
		}
		decimal num = tradingPointAccounts.Sum((TradingPointAccount item) => CalculateAccountTotalAssetInSatoshi(item, price));
		decimal num2 = CalculateAccountTotalAssetInSatoshi(tradingPointAccount, price) - (decimal)reservedHarvestSatoshi;
		decimal num3 = num * 0.12m;
		decimal num4 = num2 - num3;
		if (num4 <= 0m)
		{
			Console.WriteLine("Distribution skipped: harvest asset does not exceed 12% reserve line.");
			return;
		}
		long num5 = FloorToSatoshi(num4 * 0.20m);
		if (num5 <= 0)
		{
			Console.WriteLine("Distribution skipped: calculated satoshi amount is 0.");
			return;
		}
		long num6 = tradingPointAccount.SatoshiBalance - reservedHarvestSatoshi;
		if (num6 < num5)
		{
			Console.WriteLine("Distribution skipped: harvest satoshi balance is not enough.");
			return;
		}
		long num7 = num5 / list.Count;
		if (num7 <= 0)
		{
			Console.WriteLine("Distribution skipped: per purchase amount is 0.");
			return;
		}
		List<KChartTradingPointInfo> targetTradingPoints = list.Select((TradingPointAccount item) => item.TradingPoint).ToList();
		IReadOnlyList<KChartRouteInfo> readOnlyList = KChartRunWithFiveElementsAPI.LoadRoutesFromTradingPoint(tradingPointAccount.TradingPoint, targetTradingPoints);
		if (readOnlyList.Count != list.Count)
		{
			throw new InvalidOperationException($"Route count {readOnlyList.Count} does not match purchase account count {list.Count}.");
		}
		Dictionary<int, TradingPointAccount> circleAccounts = BuildCircleAccountMap(list);
		long num8 = checked(num7 * list.Count);
		tradingPointAccount.SatoshiBalance -= num8;
		List<RouteContractOrderResult> list2 = new List<RouteContractOrderResult>(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			TradingPointAccount tradingPointAccount2 = list[i];
			tradingPointAccount2.SatoshiBalance += num7;
			RouteContractOrderResult item2 = AddContractOrdersFromRoute(tradingPointAccount, tradingPointAccount2, readOnlyList[i], circleAccounts, tradingPointAccount, kLineIndex, currentFiveElement, price, 1m, contractMarket);
			list2.Add(item2);
		}
		Console.WriteLine($"Distribution done: total={num8}, perPurchase={num7}, purchaseCount={list.Count}.");
		Console.WriteLine($"Contract market added={list2.Sum((RouteContractOrderResult item) => item.OrderCount)}, availableFromKLineIndex={kLineIndex + 1}, marketCount={contractMarket.Count}.");
	}

	private static Dictionary<int, TradingPointAccount> BuildCircleAccountMap(IReadOnlyList<TradingPointAccount> purchaseAccounts)
	{
		Dictionary<int, TradingPointAccount> dictionary = new Dictionary<int, TradingPointAccount>(purchaseAccounts.Count);
		foreach (TradingPointAccount purchaseAccount in purchaseAccounts)
		{
			int ownerCircleIndex = purchaseAccount.TradingPoint.OwnerCircleIndex;
			if (ownerCircleIndex < 0)
			{
				throw new InvalidOperationException($"Purchase account has invalid owner circle index: {ownerCircleIndex}.");
			}
			if (!dictionary.TryAdd(ownerCircleIndex, purchaseAccount))
			{
				throw new InvalidOperationException($"Duplicate account for owner circle index {ownerCircleIndex}.");
			}
		}
		return dictionary;
	}

	private static RouteContractOrderResult AddContractOrdersFromRoute(TradingPointAccount sourceAccount, TradingPointAccount targetAccount, KChartRouteInfo route, IReadOnlyDictionary<int, TradingPointAccount> circleAccounts, TradingPointAccount harvestAccount, int kLineIndex, FiveElement currentFiveElement, decimal price, decimal pathEffectMultiplier, List<ContractOrder> contractMarket)
	{
		if (pathEffectMultiplier <= 0m)
		{
			throw new ArgumentOutOfRangeException("pathEffectMultiplier");
		}
		List<ContractOrder> list = new List<ContractOrder>();
		IReadOnlyList<KChartRoutePointInfo> routePoints = route.RoutePoints;
		if (routePoints.Count < 2)
		{
			return new RouteContractOrderResult(sourceAccount, targetAccount, route, list);
		}
		int num = 0;
		while (num < routePoints.Count - 1)
		{
			int circleIndex = routePoints[num].CircleIndex;
			int i;
			for (i = num; i + 1 < routePoints.Count && routePoints[i + 1].CircleIndex == circleIndex; i++)
			{
			}
			if (i > num)
			{
				if (!circleAccounts.TryGetValue(circleIndex, out TradingPointAccount? value))
				{
					throw new InvalidOperationException($"Route passed circle {circleIndex}, but no owner account exists.");
				}
				ContractOrder? contractOrder = TryCreateContractOrderForSegment(sourceAccount, targetAccount, value, harvestAccount, route, num, i, kLineIndex, currentFiveElement, price, pathEffectMultiplier);
				if (contractOrder != null)
				{
					value.ContractOrders.Add(contractOrder);
					contractMarket.Add(contractOrder);
					list.Add(contractOrder);
				}
			}
			num = i + 1;
		}
		return new RouteContractOrderResult(sourceAccount, targetAccount, route, list);
	}

	private static ContractOrder? TryCreateContractOrderForSegment(TradingPointAccount sourceAccount, TradingPointAccount targetAccount, TradingPointAccount circleAccount, TradingPointAccount harvestAccount, KChartRouteInfo route, int segmentStart, int segmentEnd, int kLineIndex, FiveElement currentFiveElement, decimal price, decimal pathEffectMultiplier)
	{
		IReadOnlyList<KChartRoutePointInfo> routePoints = route.RoutePoints;
		KChartRoutePointInfo kChartRoutePointInfo = routePoints[segmentStart];
		long num = CalculateArcStepCount(routePoints, segmentStart, segmentEnd);
		if (num <= 0)
		{
			return null;
		}
		decimal num2 = CalculateArcRadian(num, kChartRoutePointInfo.PathPointCount);
		decimal num3 = num2 / 6.2831853071795864769252867666m * 0.20m * pathEffectMultiplier;
		if (num3 <= 0m)
		{
			return null;
		}
		ContractDirection direction = ((kChartRoutePointInfo.SignedRadius <= 0) ? ContractDirection.Short : ContractDirection.Long);
		decimal takeProfitPrice = CalculateTakeProfitPrice(price, direction);
		decimal liquidationPrice = CalculateLiquidationPrice(price, direction, 2.71828m);
		decimal num4 = ConvertUToSatoshiValue(circleAccount.UBalance, price);
		ContractMarginAsset marginAsset;
		decimal num5;
		if (num4 > (decimal)circleAccount.SatoshiBalance)
		{
			marginAsset = ContractMarginAsset.U;
			num5 = circleAccount.UBalance * num3;
			if (num5 <= 0m)
			{
				return null;
			}
			circleAccount.UBalance -= num5;
			harvestAccount.UBalance += num5;
		}
		else
		{
			marginAsset = ContractMarginAsset.Satoshi;
			long num6 = FloorToSatoshi((decimal)circleAccount.SatoshiBalance * num3);
			if (num6 <= 0)
			{
				return null;
			}
			num5 = num6;
			circleAccount.SatoshiBalance -= num6;
			harvestAccount.SatoshiBalance += num6;
		}
		decimal nominalPosition = num5 * 2.71828m;
		int[] routePointIndexes = route.RoutePointIndexes.Skip(segmentStart).Take(segmentEnd - segmentStart + 1).ToArray();
		return new ContractOrder(sourceAccount.TradingPoint, targetAccount.TradingPoint, circleAccount.TradingPoint.OwnerCircleIndex, circleAccount.TradingPoint.OwnerCircleId, kChartRoutePointInfo.CircleIndex, kChartRoutePointInfo.CircleId, direction, marginAsset, num5, num3, num, num2, kChartRoutePointInfo.PathPointCount, kLineIndex, kLineIndex + 1, currentFiveElement, price, 2.71828m, takeProfitPrice, liquidationPrice, nominalPosition, routePointIndexes);
	}

	private static long CalculateArcStepCount(IReadOnlyList<KChartRoutePointInfo> routePoints, int segmentStart, int segmentEnd)
	{
		long num = 0L;
		for (int i = segmentStart; i < segmentEnd; i++)
		{
			KChartRoutePointInfo kChartRoutePointInfo = routePoints[i];
			KChartRoutePointInfo kChartRoutePointInfo2 = routePoints[i + 1];
			if (kChartRoutePointInfo.CircleIndex != kChartRoutePointInfo2.CircleIndex)
			{
				throw new InvalidOperationException("Route segment contains different circle indexes.");
			}
			if (kChartRoutePointInfo.PathPointCount != kChartRoutePointInfo2.PathPointCount)
			{
				throw new InvalidOperationException("Route segment contains different path point counts.");
			}
			num += CalculateForwardArcStep(kChartRoutePointInfo, kChartRoutePointInfo2);
		}
		return num;
	}

	private static decimal CalculateArcRadian(long arcStepCount, int pathPointCount)
	{
		if (arcStepCount <= 0)
		{
			throw new ArgumentOutOfRangeException("arcStepCount");
		}
		if (pathPointCount <= 0)
		{
			throw new ArgumentOutOfRangeException("pathPointCount");
		}
		return (decimal)arcStepCount * 6.2831853071795864769252867666m / (decimal)pathPointCount;
	}

	private static long CalculateForwardArcStep(KChartRoutePointInfo current, KChartRoutePointInfo next)
	{
		if (current.CircleIndex != next.CircleIndex)
		{
			throw new InvalidOperationException("Arc step can only be calculated on the same circle.");
		}
		if (current.PathPointCount <= 0)
		{
			throw new InvalidOperationException("Path point count must be greater than 0.");
		}
		if (current.PathPointCount != next.PathPointCount)
		{
			throw new InvalidOperationException("Arc step points have different path point counts.");
		}
		if (current.PointIndex < 0 || current.PointIndex >= current.PathPointCount || next.PointIndex < 0 || next.PointIndex >= current.PathPointCount)
		{
			throw new InvalidOperationException("Arc step point index is outside the circle path point range.");
		}
		if (next.PointIndex >= current.PointIndex)
		{
			return next.PointIndex - current.PointIndex;
		}
		return (long)current.PathPointCount - (long)current.PointIndex + next.PointIndex;
	}

	private static decimal CalculateTakeProfitPrice(decimal openPrice, ContractDirection direction)
	{
		return (direction == ContractDirection.Long) ? (openPrice * 46m / 45m) : (openPrice * 45m / 46m);
	}

	private static decimal CalculateLiquidationPrice(decimal openPrice, ContractDirection direction, decimal leverage)
	{
		decimal num = 1m / leverage;
		return (direction == ContractDirection.Long) ? (openPrice * (1m - num)) : (openPrice * (1m + num));
	}

	private static decimal ConvertUToSatoshiValue(decimal uAmount, decimal price)
	{
		return uAmount / price * 100000000m;
	}

	private static decimal ConvertSatoshiToU(long satoshiAmount, decimal price)
	{
		return (decimal)satoshiAmount / 100000000m * price;
	}

	private static decimal ConvertSatoshiValueToU(decimal satoshiValue, decimal price)
	{
		return satoshiValue / 100000000m * price;
	}

	private static long FloorToSatoshi(decimal value)
	{
		decimal num = Math.Floor(value);
		if (num > 9223372036854775807m)
		{
			throw new OverflowException("Satoshi value is greater than long.MaxValue.");
		}
		if (num < -9223372036854775808m)
		{
			throw new OverflowException("Satoshi value is less than long.MinValue.");
		}
		return (long)num;
	}

	private static IReadOnlyList<KLine> ConvertToKLines(IReadOnlyList<dataItem> dataItems)
	{
		List<KLine> list = new List<KLine>(dataItems.Count);
		foreach (dataItem dataItem in dataItems)
		{
			list.Add(new KLine(dataItem.dateTime, dataItem.openValue, dataItem.highValue, dataItem.lowValue, dataItem.closeValue, dataItem.volumeValue));
		}
		return list;
	}

	private static bool RunLoop()
	{
		if (File.Exists("stop.bin"))
		{
			Console.WriteLine($"检测到 {StopFileName}，程序停止。");
			return false;
		}
		return true;
	}

	private static List<TradingPointAccount> CreateTradingPointAccounts(IReadOnlyList<KChartTradingPointInfo> tradingPoints)
	{
		List<TradingPointAccount> list = new List<TradingPointAccount>(tradingPoints.Count);
		foreach (KChartTradingPointInfo tradingPoint in tradingPoints)
		{
			if (tradingPoint.PointKind == KChartTradingPointKind.Harvest)
			{
				list.Add(new TradingPointAccount(tradingPoint, 210000000000L, 1000m));
				continue;
			}
			if (tradingPoint.PointKind == KChartTradingPointKind.Purchase)
			{
				list.Add(new TradingPointAccount(tradingPoint, 0L, 1000m));
				continue;
			}
			throw new InvalidOperationException($"Unknown trading point kind: {tradingPoint.PointKind}.");
		}
		return list;
	}

	private static List<TradingPointAccount> RebuildTradingPointAccountsAfterCircleMaintenance(IReadOnlyList<TradingPointAccount> oldAccounts, IReadOnlyList<KChartTradingPointInfo> reloadedTradingPoints, List<SpotOrder> spotMarket, List<ContractOrder> contractMarket, KLine? liquidationKLine, IReadOnlyDictionary<TradingPointAccount, long> oldPendingHarvestReturns, out Dictionary<TradingPointAccount, long> rebuiltPendingHarvestReturns)
	{
		if (oldAccounts == null)
		{
			throw new ArgumentNullException("oldAccounts");
		}
		if (reloadedTradingPoints == null)
		{
			throw new ArgumentNullException("reloadedTradingPoints");
		}
		if (oldPendingHarvestReturns == null)
		{
			throw new ArgumentNullException("oldPendingHarvestReturns");
		}
		if (spotMarket == null)
		{
			throw new ArgumentNullException("spotMarket");
		}
		if (contractMarket == null)
		{
			throw new ArgumentNullException("contractMarket");
		}
		TradingPointAccount tradingPointAccount = oldAccounts.Single((TradingPointAccount item) => item.PointKind == KChartTradingPointKind.Harvest);
		Dictionary<string, TradingPointAccount> dictionary = (from item in oldAccounts
			where item.PointKind == KChartTradingPointKind.Purchase
			group item by item.TradingPoint.OwnerCircleId).ToDictionary((IGrouping<string, TradingPointAccount> item) => item.Key, (IGrouping<string, TradingPointAccount> item) => item.Single());
		HashSet<string> reloadedPurchaseOwnerCircleIds = (from item in reloadedTradingPoints
			where item.PointKind == KChartTradingPointKind.Purchase
			select item.OwnerCircleId).ToHashSet<string>(StringComparer.Ordinal);
		foreach (TradingPointAccount item in dictionary.Values.Where((TradingPointAccount item) => !reloadedPurchaseOwnerCircleIds.Contains(item.TradingPoint.OwnerCircleId)).ToList())
		{
			LiquidateBankruptAccount(item, tradingPointAccount, spotMarket, contractMarket, liquidationKLine);
		}
		List<TradingPointAccount> list = new List<TradingPointAccount>(reloadedTradingPoints.Count);
		rebuiltPendingHarvestReturns = new Dictionary<TradingPointAccount, long>();
		foreach (KChartTradingPointInfo reloadedTradingPoint in reloadedTradingPoints)
		{
			if (reloadedTradingPoint.PointKind == KChartTradingPointKind.Harvest)
			{
				tradingPointAccount.UpdateTradingPoint(reloadedTradingPoint);
				list.Add(tradingPointAccount);
				continue;
			}
			if (reloadedTradingPoint.PointKind != KChartTradingPointKind.Purchase)
			{
				throw new InvalidOperationException($"Unknown trading point kind: {reloadedTradingPoint.PointKind}.");
			}
			TradingPointAccount tradingPointAccount2;
			if (dictionary.TryGetValue(reloadedTradingPoint.OwnerCircleId, out var value) && !value.IsBankrupt)
			{
				value.UpdateTradingPoint(reloadedTradingPoint);
				tradingPointAccount2 = value;
				if (oldPendingHarvestReturns.TryGetValue(value, out var value2) && value2 > 0)
				{
					rebuiltPendingHarvestReturns.Add(tradingPointAccount2, value2);
				}
			}
			else
			{
				tradingPointAccount2 = new TradingPointAccount(reloadedTradingPoint, 0L, 1000m);
			}
			list.Add(tradingPointAccount2);
		}
		return list;
	}

	private static void ClearAccountOrderLists(IReadOnlyList<TradingPointAccount> tradingPointAccounts)
	{
		if (tradingPointAccounts == null)
		{
			throw new ArgumentNullException("tradingPointAccounts");
		}
		foreach (TradingPointAccount tradingPointAccount in tradingPointAccounts)
		{
			tradingPointAccount.SpotOrders.Clear();
			tradingPointAccount.ContractOrders.Clear();
		}
	}

	private static void PrintMenu()
	{
		Console.WriteLine();
		Console.WriteLine("请输入命令：");
		Console.WriteLine("SAMPLE       ----------运行内置K线五行样例");
		Console.WriteLine("CLASSIFYCSV  ----------读取CSV并计算第25根及以后K线五行");
		Console.WriteLine("EXIT         ----------退出");
	}

	private static void RunSample()
	{
		foreach (KeyValuePair<string, IReadOnlyList<KLine>> item in SampleKLines.BuildAllSamples())
		{
			KLineFiveElementResult value = FiveElementClassifier.ClassifyNext(item.Value, 0);
			Console.WriteLine($"{item.Key}样例 -> {value}");
		}
		Console.WriteLine();
		Console.WriteLine("五行关系样例：");
		Console.WriteLine($"金 与 水：{FiveElementRelationCalculator.GetRelation(FiveElement.Metal, FiveElement.Water)}");
		Console.WriteLine($"金 与 木：{FiveElementRelationCalculator.GetRelation(FiveElement.Metal, FiveElement.Wood)}");
		Console.WriteLine($"金 与 土：{FiveElementRelationCalculator.GetRelation(FiveElement.Metal, FiveElement.Earth)}");
	}

	private static void ClassifyCsv()
	{
		Console.WriteLine("请输入CSV文件路径。字段支持 dateTime/open/high/low/close/volume，或无表头：时间,开,高,低,收,量。");
		string? text = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(text))
		{
			Console.WriteLine("CSV路径不能为空。");
			return;
		}
		string path = text.Trim().Trim('"');
		IReadOnlyList<KLine> readOnlyList = KLineCsvReader.Read(path);
		IReadOnlyList<KLineFiveElementResult> readOnlyList2 = FiveElementClassifier.ClassifyAll(readOnlyList);
		Console.WriteLine($"K线数量：{readOnlyList.Count}");
		Console.WriteLine($"可计算五行数量：{readOnlyList2.Count}");
		foreach (KLineFiveElementResult item in readOnlyList2)
		{
			Console.WriteLine(item);
		}
	}
}

