namespace CodeSmellApp;

public class NamingRuleViolator
{
    private const int BadConstant1 = 1;
    private int BadCounter;

    public NamingRuleViolator(int startingValue)
    {
        BadCounter = startingValue + BadConstant1;
    }

    public void Increment()
    {
        BadCounter++;
    }

    public int GetCurrentValue()
    {
        return BadCounter;
    }
}