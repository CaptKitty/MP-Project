using System.Text;

public static class UIProvinceEconomySummary
{
    public static string Build(Province province)
    {
        if (province == null) return "Provincial Output\nNo province";
        return new StringBuilder("Gold output: ").Append(province.GetGoldIncome())
            .Append("\nFood output: ").Append(province.GetFoodOutput())
            .ToString();
    }
}
