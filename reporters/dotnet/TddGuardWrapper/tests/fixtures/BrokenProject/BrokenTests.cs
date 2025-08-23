using System;
using Xunit;

namespace BrokenProject.Tests;

public class BrokenTest
{
    [Fact]
    public void TestThatWouldPassIfItCompiled()
    {
        // This method has multiple compilation errors
        var result = SomeNonExistentMethod(); // CS0103: The name 'SomeNonExistentMethod' does not exist
        
        string missingVariable // CS1002: ; expected
        
        UnknownType unknownVar = new UnknownType(); // CS0246: The type or namespace name 'UnknownType' could not be found
        
        Assert.True(true);
    }
    
    [Fact
    public void AnotherBrokenTest() // CS1513: } expected (missing closing bracket on attribute)
    {
        Console.WriteLine("This won't compile");
    }
}
