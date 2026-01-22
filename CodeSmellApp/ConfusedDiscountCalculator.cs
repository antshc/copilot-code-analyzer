namespace CodeSmellApp;

public class ConfusedDiscountCalculator
{
    public decimal Calculate(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (amount >= 100)
        {
            return amount * 0.9m;
        }

        return amount;
    }
}