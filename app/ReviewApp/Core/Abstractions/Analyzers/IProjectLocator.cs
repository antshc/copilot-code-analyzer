namespace ReviewApp.Core.Analyzers;

public interface IProjectLocator
{
    string FindProjectForFile(string relativeSourceFile);
}
