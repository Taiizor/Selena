# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take the security of Selena seriously. If you have discovered a security vulnerability, please follow these steps:

1. **DO NOT** create a public GitHub issue for the vulnerability.
2. Send a detailed report to: taiizor@vegalya.com
3. Include the following in your report:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

## Response Timeline

- **Initial Response**: Within 48 hours
- **Assessment**: Within 1 week
- **Fix Timeline**: Depends on severity
  - Critical: Within 24 hours
  - High: Within 1 week
  - Medium: Within 2 weeks
  - Low: Next release

## Security Best Practices

When using Selena:

1. **Always use the latest version** - Security patches are included in new releases
2. **Use Local scope by default** - Only use Global scope when necessary
3. **Validate message content** - Always validate deserialized objects
4. **Secure your channel names** - Use unique, hard-to-guess channel names
5. **Monitor buffer usage** - Prevent DoS attacks by monitoring buffer consumption

## Known Security Considerations

- Memory-mapped files can be accessed by any process with appropriate permissions
- Global scope on Windows requires administrator privileges
- No built-in encryption - sensitive data should be encrypted at the application level
- No built-in authentication - implement your own if needed

## Disclosure Policy

- Security vulnerabilities will be disclosed after a fix is available
- Users will be notified via GitHub Security Advisories
- A CVE will be requested for critical vulnerabilities

## Contact

- Security Email: taiizor@vegalya.com
- GPG Key: [Public Key ID]

Thank you for helping keep Selena and its users safe!
