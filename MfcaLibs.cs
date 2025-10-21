using System;
using System.IO;
using System.Text;

/**************************************************************************************************
 * Author: Mahler Chou 周敬斐
 * File: MfcaLibs.cs
 * Version: v1
 * Description: This code is intended to illustrate the calculation procedures as outlined 
 *              in the ISO 14053:2021(E) standard.
 *************************************************************************************************/

/// <summary>
/// Target Material
/// </summary>
public class TargetMaterial
{
    public string MaterialName { get; private set; }
    public decimal UnitPrice { get; private set; } = 0;
    public decimal ProductQuantity { get; private set; }
    public decimal LossQuantity { get; private set; }

    /// <summary>
    /// If set to false, the material will not participate material allocation.
    /// </summary>
    public bool ParticipateAllocation { get; private set; }

    public TargetMaterial(string material_name, decimal unit_price, decimal product_quantity, decimal loss_quantity, bool participate_allocation)
    {
        MaterialName = material_name;
        UnitPrice = unit_price;
        ProductQuantity = product_quantity;
        LossQuantity = loss_quantity;
        ParticipateAllocation = participate_allocation;
    }

    public string GetMaterialDisplayName => (ParticipateAllocation ? "" : "* ") + MaterialName;
    public decimal ProductCost => UnitPrice * ProductQuantity;
    public decimal LossCost => UnitPrice * LossQuantity;
    public decimal Input => ProductQuantity + LossQuantity;
    public decimal Cost => Input * UnitPrice;
    public decimal LossRatio => LossQuantity / Input;
    public decimal ProductRatio => ProductQuantity / Input;
}

/// <summary>
/// Waste Management
/// </summary>
public class WasteMgmtCost
{
    public string ItemName { get; private set; }
    public decimal Cost { get; private set; }
    public WasteMgmtCost(string item_name, decimal cost)
    {
        ItemName = item_name;
        Cost = cost;
    }
}

/// <summary>
/// Energy Cost
/// </summary>
public class EnergyCost
{
    public string ItemName { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal Cost { get; private set; }

    /// <summary>
    /// Cost are calcuated by unit_price and quantity
    /// </summary>
    public EnergyCost(string item_name, decimal unit_price, decimal quantity)
    {
        ItemName = item_name;
        UnitPrice = unit_price;
        Quantity = quantity;
        Cost = unit_price * quantity;
    }

    public EnergyCost(string itemName, decimal cost)
    {
        ItemName = itemName;
        Cost = cost;
    }
}

/// <summary>
/// System Cost
/// </summary>
public class SystemCost
{
    public string ItemName { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal Cost { get; private set; }

    public SystemCost(string item_name, decimal unit_price, decimal quantity)
    {
        ItemName = item_name;
        UnitPrice = unit_price;
        Quantity = quantity;
        Cost = unit_price * quantity;
    }

    public SystemCost(string item_name, decimal cost)
    {
        ItemName = item_name;
        Cost = cost;
    }
}

/// <summary>
/// Process (aka Quantity Center in ISO 14051)
/// </summary>
public class Process
{
    public string ProcessName { get; private set; }
    public string ProductionStartDay { get; private set; }
    public string ProductionPeriod { get; private set; }
    public decimal PlannedVolume { get; private set; }

    private List<TargetMaterial> _materialCosts = new();
    private List<WasteMgmtCost> _wasteMgmtCost = new();
    private List<EnergyCost> _energyCosts = new();
    private List<SystemCost> _systemCosts = new();

    public Process(string process_name, string start_day, string production_period, decimal planned_volume)
    {
        ProcessName = process_name;
        ProductionStartDay = start_day;
        ProductionPeriod = production_period;
        PlannedVolume = planned_volume;
    }

    #region DATA APIs

    /// <summary>
    /// Add the material input item to the process.
    /// </summary>
    /// <param name="Material name"></param>
    /// <param name="Unit price"></param>
    /// <param name="Product quantity"></param>
    /// <param name="Loss quantity (NG)"></param>
    /// <param name="Indicate if the material will participate cost allocation"></param>
    public void SetTargetMaterial(string material_name, decimal unit_price, decimal product_quantity, decimal loss_quantity, bool participate_allocation = true)
    {
        _materialCosts.Add(new TargetMaterial(material_name, unit_price, product_quantity, loss_quantity, participate_allocation));
    }

    /// <summary>
    /// Add the waste item to the process.
    /// Where waste materials have an assigned unit price and quantity, the total value must be calculated before entry.
    /// </summary>
    /// <param name="Waste item name"></param>
    /// <param name="The waste management cost"></param>
    public void SetWasteMgmtCost(string ItemName, decimal Cost)
    {
        _wasteMgmtCost.Add(new WasteMgmtCost(ItemName, Cost));
    }

    /// <summary>
    /// Add the energy cost item to the process
    /// </summary>
    /// <param name="Energy item"></param>
    /// <param name="The cost of energy item"></param>
    public void SetEnergyCost(string ItemName, decimal Cost)
    {
        _energyCosts.Add(new EnergyCost(ItemName, Cost));
    }

    /// <summary>
    /// Add the system cost item to the process
    /// </summary>
    /// <param name="System cost item"></param>
    /// <param name="The cost of the system item"></param>
    public void SetSystemCost(string ItemName, decimal Cost)
    {
        _systemCosts.Add(new SystemCost(ItemName, Cost));
    }

    /// <summary>
    /// This is a helper method to import all CSV data at once.
    /// </summary>
    /// <param name="Process name"></param>
    /// <param name="Production start day"></param>
    /// <param name="Production period"></param>
    /// <param name="Planned quantity"></param>
    /// <param name="CSV file path"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Process LoadFromCSV(string process_name, string start_day, string production_period, decimal planned_quantity, string csv_path) 
    {        
        if (!File.Exists(Path.GetFullPath(csv_path)))
        {
            throw new InvalidOperationException($"File {csv_path} does not exist.");
        }

        Process p = new Process(process_name, start_day, production_period, planned_quantity);

        string csv_body = File.ReadAllText(Path.GetFullPath(csv_path));
        string[] csv_lines = csv_body.Split('\n');
        for(int i=1; i<csv_lines.Length; i++)
        {
            if (string.IsNullOrEmpty(csv_lines[i].Trim())) {
                continue;
            }

            string[] f = csv_lines[i].Split(',');
            if (f.Length != 7)
            {
                Console.WriteLine($"Un support format, skip line {i + 1}.");
                continue;
            }

            decimal unit_price = 0;
            decimal quantity = 0;
            decimal cost = 0;
            decimal product_qty = 0;
            decimal loss_qty = 0;

            switch (f[0])
            {
                case "MTL":
                    unit_price = ToDecimal(f[2]);
                    quantity = ToDecimal(f[3]);
                    cost = ToDecimal(f[4]);
                    product_qty = ToDecimal(f[5]);
                    loss_qty = ToDecimal(f[6]);
                    p.SetTargetMaterial(f[1], unit_price, product_qty, loss_qty, true);
                    break;
                case "MTLN":
                    unit_price = ToDecimal(f[2]);
                    quantity = ToDecimal(f[3]);
                    cost = ToDecimal(f[4]);
                    product_qty = ToDecimal(f[5]);
                    loss_qty = ToDecimal(f[6]);
                    p.SetTargetMaterial(f[1], unit_price, product_qty, loss_qty, false);
                    break;
                case "WM":
                    cost = ToDecimal(f[4]);
                    p.SetWasteMgmtCost(f[1], cost);
                    break;
                case "EC":
                    cost = ToDecimal(f[4]);
                    p.SetEnergyCost(f[1], cost);
                    break;
                case "SC":
                    cost = ToDecimal(f[4]);
                    p.SetSystemCost(f[1], cost);
                    break;
                default:
                    Console.WriteLine($"Unknow type {f[0]}, skipped");
                    break;
            }
        }

        return p;
    }

    private static decimal ToDecimal(string? str)
    {
        if (str == null) return 0;
        if (string.IsNullOrEmpty(str)) return 0;
        return Convert.ToDecimal(str);
    }

    #endregion

    #region Calculators

    public decimal SumMaterialCost => _materialCosts.Sum(m => m.Cost);
    public decimal SumMaterialProductCost => _materialCosts.Sum(m => m.ProductCost);
    public decimal SumMaterialLossCost => _materialCosts.Sum(_ => _.LossCost);

    public decimal SumMaterialProductQuantity => _materialCosts.Sum(x => x.ProductQuantity);
    public decimal SumMaterialLossQuantity => _materialCosts.Sum(x => x.LossQuantity);

    public decimal ProductAllocationRatio
    {
        get
        {
            decimal total_product = 0;
            decimal total_input = 0;
            foreach (var item in _materialCosts)
            {
                if (item.ParticipateAllocation)
                {
                    total_product += item.ProductQuantity;
                    total_input += item.Input;
                }
            }

            if (total_input == 0) return 0;

            // Round to 99.9%
            decimal result = Math.Round(total_product / total_input, 3);
            return result;
        }
    }

    public decimal LossAllocationRatio => (1 - ProductAllocationRatio);

    public decimal SumWasteMgmtCost => _wasteMgmtCost.Sum(w => w.Cost);

    public decimal SumEnergyCost => _energyCosts.Sum(x => x.Cost);
    public decimal SumEnergyProductCost => SumEnergyCost * ProductAllocationRatio;
    public decimal SumEnergyLossCost => SumEnergyCost * LossAllocationRatio;

    public decimal SumSystemCost => _systemCosts.Sum(x => x.Cost);
    public decimal SumSystemProductCost => SumSystemCost * ProductAllocationRatio;
    public decimal SumSystemLossCost => SumSystemCost * LossAllocationRatio;

    public decimal TotalCost => SumMaterialCost + SumWasteMgmtCost + SumEnergyCost + SumSystemCost;
    public decimal TotalProductCost => SumMaterialProductCost + SumEnergyProductCost + SumSystemProductCost;
    public decimal TotalLossCost => SumMaterialLossCost + SumWasteMgmtCost + SumEnergyLossCost + SumSystemLossCost;

    #endregion

    #region TOSTRINGs

    string ToString_Material()
    {
        StringBuilder str = new();

        str.AppendLine("(TARGET MATERIALS)");
        str.AppendLine($"{"Material Name",-20} {"Unit Price",-10} {"Input",-10} {"Cost",-10} | {"Product",-10} {"Cost",-10} | {"Loss",-10} {"Cost",-10} | {"Loss Ratio",-10} ");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10}");
        foreach (TargetMaterial i in _materialCosts)
        {
            str.AppendLine($"{i.GetMaterialDisplayName,-20} {i.UnitPrice,10} {i.Input,10} {i.Cost,10} | {i.ProductQuantity,10} {i.ProductCost,10} | {i.LossQuantity,10} {i.LossCost,10} | {i.LossRatio.ToString("P1"),10}");
        }
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | ");
        str.AppendLine($"{"Subtotal of MTL.",-20} {"          ",10} {SumMaterialProductQuantity + SumMaterialLossQuantity,10} {SumMaterialCost,10} | {SumMaterialProductQuantity,10} {SumMaterialProductCost,10} | {SumMaterialLossQuantity,10} {SumMaterialLossCost,10} | ");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | ");
        str.AppendLine($"{"Allocation Ratio",-20} {"          ",10} {"          ",10} {"          ",10} | {ProductAllocationRatio.ToString("P1"),10} {"          ",10} | {LossAllocationRatio.ToString("P1"),10} {"          ",10} | {"          ",10}");
        return str.ToString();
    }

    string ToString_WasteMgmt()
    {
        StringBuilder str = new();

        //str.AppendLine($"{"Allocation Ratio",-20}: Product({ProductAllocationRatio.ToString("P1")}) / Loss({LossAllocationRatio.ToString("P1")})");
        str.AppendLine("(WASTE MANAGEMENT)");
        str.AppendLine($"{"Item Name",-20} {"",-10} {"",-10} {"",-10} | {"",-10} {"",-10} | {"",-10} {"Cost",-10}");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");
        foreach (WasteMgmtCost w in _wasteMgmtCost)
        {
            str.AppendLine($"{w.ItemName,-20} {"",-10} {"",-10} {"",-10} | {"",-10} {"",-10} | {"",-10} {w.Cost,-10}");
        }
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");
        str.AppendLine($"{"Subtotal",-20} {"",-10} {"",-10} {"",-10} | {"",-10} {"",-10} | {"",-10} {SumWasteMgmtCost,-10}");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");

        return str.ToString();
    }

    string ToString_EnergyCost()
    {
        StringBuilder str = new();

        str.AppendLine("(ENERGY COSTS)");
        str.AppendLine($"{"Item Name",-20} {"Unit Price",-10} {"Quantity",-10} {"Cost",-10} | {"Ratio(+)",-10} {"Cost",-10} | {"Ratio(-)",-10} {"Cost",-10}");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");
        foreach (EnergyCost i in _energyCosts)
        {
            str.AppendLine($"{i.ItemName,-20} {i.UnitPrice,10} {i.Quantity,10} {i.Cost,10} | {ProductAllocationRatio.ToString("P1"),10} {(i.Cost * ProductAllocationRatio).ToString("0"),10} | {LossAllocationRatio.ToString("P1"),10} {(i.Cost * LossAllocationRatio).ToString("0"),10}");
        }
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");
        str.AppendLine($"{"SUBTOTAL",-20} {"",10} {"",10} {SumEnergyCost.ToString("#,##0"),10} | {"",10} {SumEnergyProductCost.ToString("#,##0"),10} | {"",10} {SumEnergyLossCost.ToString("#,##0"),10}");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");

        return str.ToString();
    }

    string ToString_SystemCost()
    {
        StringBuilder str = new();

        str.AppendLine("(SYSTEM COSTS)");
        str.AppendLine($"{"Item Name",-20} {"Unit Price",-10} {"Quantity",-10} {"Cost",-10} | {"Ratio(+)",-10} {"Cost",-10} | {"Ratio(-)",-10} {"Cost",-10}");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");
        foreach (SystemCost i in _systemCosts)
        {
            str.AppendLine($"{i.ItemName,-20} {i.UnitPrice,10} {i.Quantity,10} {i.Cost,10} | {ProductAllocationRatio.ToString("P1"),10} {(i.Cost * ProductAllocationRatio).ToString("0"),10} | {LossAllocationRatio.ToString("P1"),10} {(i.Cost * LossAllocationRatio).ToString("0"),10}");
        }
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");
        str.AppendLine($"{"SUBTOTAL",-20} {"",10} {"",10} {SumSystemCost.ToString("#,##0"),10} | {ProductAllocationRatio.ToString("P1"),10} {SumSystemProductCost.ToString("#,##0"),10} | {LossAllocationRatio.ToString("P1"),10} {SumSystemLossCost.ToString("#,##0"),10}");
        str.AppendLine($"{"--------------------",-20} {"----------",10} {"----------",10} {"----------",10} | {"----------",10} {"----------",10} | {"----------",10} {"----------",10}");

        return str.ToString();
    }

    string ToString_Report()
    {
        StringBuilder _ = new();

        _.AppendLine("==================================================================================================================");
        _.AppendLine($"Selected production process: {this.ProcessName}");
        _.AppendLine($"Production period: {this.ProductionStartDay} - {this.ProductionPeriod}");
        _.AppendLine($"Planned production volumn: {this.PlannedVolume}");
        _.AppendLine("==================================================================================================================");
        
        _.AppendLine(ToString_Material());
        _.AppendLine(ToString_WasteMgmt());
        _.AppendLine(ToString_EnergyCost());
        _.AppendLine(ToString_SystemCost());

        _.AppendLine("==================================================================================================================");
        _.AppendLine($"{"TOTAL COST",-20} {"",10} {"",10} {
            (TotalCost).ToString("#,##0"),10} | {"",10} {
            (TotalProductCost).ToString("#,##0"),10} | {"",10} {
            (TotalLossCost).ToString("#,##0"),10}");
        _.AppendLine($"{"",-20} {"",10} {"",10} {"",10} | {"",10} {(TotalProductCost / TotalCost).ToString("P1"),10} | {"",10} {(TotalLossCost / TotalCost).ToString("P1"),10}");
        return _.ToString();
    }
    #endregion

    #region Report commands
    public void ShowMaterial()
    {
        Console.Write(ToString_Material());
    }

    public void ShowWasteMgmt()
    {
        Console.Write(ToString_WasteMgmt());
    }

    public void ShowEnergyCost()
    {
        Console.Write(ToString_EnergyCost());
    }

    public void ShowSystemCost()
    {
        Console.Write(ToString_SystemCost());
    }

    public void Report()
    {
        Console.Write(ToString_Report());
    }
    #endregion
}