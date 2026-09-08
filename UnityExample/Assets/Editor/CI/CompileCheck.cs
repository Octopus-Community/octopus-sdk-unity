using UnityEditor;

// Batchmode compile gate: `-executeMethod CompileCheck.Run` only runs after Unity has finished
// importing and compiling the project, so a compile error fails the batchmode invocation before
// this method is ever reached. Exiting 0 here just confirms that point was reached.
//
// Lives under Assets/Editor/ like every other Editor-only script in this project (no asmdef
// here, same as BuildScript.cs) so it is never part of a player build.
public static class CompileCheck
{
    public static void Run() => EditorApplication.Exit(0);
}
