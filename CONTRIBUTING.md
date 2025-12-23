# Contributing to Selena

First off, thank you for considering contributing to Selena! It's people like you that make Selena such a great tool.

## Code of Conduct

This project and everyone participating in it is governed by the [Selena Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues as you might find out that you don't need to create one. When you are creating a bug report, please include as many details as possible:

* **Use a clear and descriptive title**
* **Describe the exact steps to reproduce the problem**
* **Provide specific examples to demonstrate the steps**
* **Describe the behavior you observed and expected**
* **Include screenshots if applicable**
* **Include your environment details** (OS, .NET version, etc.)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

* **Use a clear and descriptive title**
* **Provide a detailed description of the proposed enhancement**
* **Provide specific use cases**
* **Explain why this enhancement would be useful**

### Pull Requests

1. Fork the repo and create your branch from `develop`
2. If you've added code that should be tested, add tests
3. If you've changed APIs, update the documentation
4. Ensure the test suite passes
5. Make sure your code follows the existing style
6. Issue that pull request!

## Development Process

### Setting Up Your Environment

1. Fork and clone the repository
```bash
git clone https://github.com/Taiizor/Selena.git
cd Selena
```

2. Create a new branch
```bash
git checkout -b feature/your-feature-name
```

3. Make your changes and test
```bash
dotnet build
dotnet test
```

### Coding Standards

* Use meaningful variable and method names
* Follow C# coding conventions
* Add XML documentation to public APIs
* Keep methods small and focused
* Write unit tests for new functionality
* Ensure no compiler warnings

### Testing

* Write unit tests for all new code
* Ensure all tests pass before submitting PR
* Include integration tests for complex scenarios
* Aim for high code coverage (>80%)

Example test structure:
```csharp
[TestClass]
public class YourFeatureTests
{
    [TestMethod]
    public void YourMethod_WhenCondition_ShouldExpectedBehavior()
    {
        // Arrange
        var sut = new YourClass();
        
        // Act
        var result = sut.YourMethod();
        
        // Assert
        Assert.IsNotNull(result);
    }
}
```

### Commit Messages

* Use the present tense ("Add feature" not "Added feature")
* Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
* Limit the first line to 72 characters or less
* Reference issues and pull requests liberally after the first line

Example:
```
Add support for message compression

- Implement GZIP compression for messages over 1KB
- Add CompressionMode configuration option
- Update documentation with compression examples

Fixes #123
```

### Documentation

* Update README.md if needed
* Add XML comments to public APIs
* Update the wiki for major features
* Include examples in documentation

### Performance Considerations

* Benchmark significant changes
* Avoid allocations in hot paths
* Use object pooling where appropriate
* Profile before and after changes

## Release Process

1. Update version numbers
2. Update CHANGELOG.md
3. Create a release branch
4. Run full test suite
5. Create GitHub release
6. Publish to NuGet

## Questions?

Feel free to contact the maintainers if you have any questions. We're here to help!

## Recognition

Contributors will be recognized in:
* The README.md file
* Release notes
* Project website

Thank you for contributing to Selena! 🚀