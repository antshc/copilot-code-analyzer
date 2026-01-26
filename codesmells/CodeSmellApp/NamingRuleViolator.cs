namespace CodeSmellApp;

public class NamingRuleViolator
{
    private const int BadConstant = 1;
    private int BadCounter;

    public NamingRuleViolator(int startingValue)
    {
        BadCounter = startingValue + BadConstant;
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