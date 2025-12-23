// Disable parallel execution to avoid shared memory conflicts in IPC tests
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 1)]

namespace Selena.Tests
{
    [TestClass]
    public static class TestSetup
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            // Assembly-level initialization
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // Assembly-level cleanup
        }
    }
}