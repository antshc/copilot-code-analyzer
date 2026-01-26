using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CodeSmellApp;

public class ConfusedDiscountCalculator
{
    public decimal Calculate(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount >= 100)
        {
            return amount * 0.9m;
        }

        return amount;
    }
}
