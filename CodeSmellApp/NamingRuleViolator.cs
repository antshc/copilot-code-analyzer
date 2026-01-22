namespace CodeSmellApp;

public class NamingRuleViolator
{
    private int m_badCounter;

    public NamingRuleViolator(int startingValue)
    {
        m_badCounter = startingValue;
    }

    public void Increment()
    {
        m_badCounter++;
    }
}
