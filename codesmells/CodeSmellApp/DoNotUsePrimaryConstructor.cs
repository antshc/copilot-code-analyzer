namespace CodeSmellApp
{
    public class UsePrimaryConstructor(int myParam, string myNextParam)
    {
        public int MyParam { get; } = myParam;
        public string MyNextParam { get; } = myNextParam;
    }
}
