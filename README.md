# AI Prompt based Web Test Automation Tool

A .NET/C# application that enables automated web testing using natural language prompts. The application uses Playwright for modern browser automation and OpenAI GPT models for intelligent prompt parsing.

## What This Project Does

This tool converts natural language instructions into automated web browser actions. Simply describe what you want to test in plain English, and the application will:

1. **Parse your instructions** using OpenAI GPT for intelligent understanding
2. **Execute automated actions** like clicking buttons, filling forms, and navigating pages
3. **Generate detailed reports** of test execution results
4. **Control modern browsers** through Playwright's reliable automation

## Features

- **AI-Powered Parsing**: Uses OpenAI GPT-3.5-turbo to understand complex natural language test instructions
- **Modern Browser Automation**: Powered by Playwright for reliable, fast test execution
- **Intelligent Element Discovery**: Automatically identifies web elements using multiple search strategies
- **Multi-Action Support**: Handles navigation, clicking, typing, waiting, and verification actions
- **Smart Caching**: Caches parsed prompts to minimize API calls and costs
- **Comprehensive Logging**: Detailed execution logs and test reporting
- **JSON Report Generation**: Structured test results for analysis and CI/CD integration

## Prerequisites

- **.NET 9.0** or later
- **OpenAI API Key** (required for AI-powered parsing)
- Playwright will automatically download and manage browser binaries

## Installation & Setup

### 1. Clone and Build

```bash
# Clone or download the project
git clone <repository-url>
cd WebTestAutomation

# Restore dependencies
dotnet restore

# Build the project
dotnet build
```

### 2. OpenAI API Key Setup (Required)

1. **Get an OpenAI API Key**:
   - Visit [OpenAI API Keys](https://platform.openai.com/api-keys)
   - Sign up or log in to your OpenAI account
   - Create a new API key
   - Copy the key (starts with `sk-`)

2. **Set the Environment Variable**:

   **Windows (Command Prompt):**
   ```cmd
   set OPENAI_API_KEY=sk-your-api-key-here
   ```

   **Windows (PowerShell):**
   ```powershell
   $env:OPENAI_API_KEY="sk-your-api-key-here"
   ```

   **Linux/macOS:**
   ```bash
   export OPENAI_API_KEY=sk-your-api-key-here
   ```

   **Permanent Setup (Windows):**
   1. Open System Properties → Advanced → Environment Variables
   2. Add new system variable: `OPENAI_API_KEY` = `sk-your-api-key-here`

### 3. Verify Installation

```bash
dotnet run
```

You should see:
- ✓ AI-powered parsing enabled (OpenAI API key found)

## Usage

### Quick Start

1. **Run the application**:
   ```bash
   dotnet run
   ```

2. **Select browser mode**:
   - **Headless mode**: Browser runs invisibly (recommended for CI/CD)
   - **Headed mode**: See the browser in action (useful for debugging)

3. **Enter natural language test instructions** such as:
   - `"navigate to https://example.com, then click Accept cookies"`
   - `"login using admin@test.com with password secret123"`
   - `"go to products and create a product with name 'Test Product'"`
   - `"type 'John Doe' into the name field, then click submit"`
   - `"verify that 'Welcome' appears on the page"`

4. **Review parsed actions** - The tool shows you exactly what it plans to do

5. **Confirm execution** - Type 'y' to proceed or 'n' to skip

6. **View results** - Real-time logs and final test report

### Environment Variables

| Variable | Purpose | Required |
|----------|---------|----------|
| `OPENAI_API_KEY` | OpenAI API key for intelligent parsing | Yes |

## Example Test Scenarios

### Login Scenario
```
navigate to https://myapp.com, click Accept cookies, login using john.doe@example.com with password mypassword123, then verify that Dashboard appears
```

### E-commerce Testing
```
navigate to https://mystore.com, click on products, click create product, type 'Test Product' in name field, click save, then verify that 'Test Product' appears in product list
```

### Form Filling with Verification
```
go to contact page, type 'John Smith' in name field, type 'john@example.com' in email field, click submit, then verify that 'Thank you' appears on page
```

### Complex Multi-Step Workflow
```
navigate to https://admin.example.com, login using admin@test.com with password secret123, go to users, click add user, type 'Jane Doe' in full name, type 'jane@test.com' in email, click save, verify that 'User created successfully' appears
```

## Supported Actions

The tool supports these action types:

| Action Type | Keywords | Purpose | Example |
|-------------|----------|---------|---------|  
| **Navigate** | `navigate`, `go to` (URLs only) | Navigate to a specific URL | `navigate to https://example.com` |
| **Click** | `click`, `press`, `tap` | Click buttons, links, or navigate to sections | `click login button` |
| **Type** | `type`, `enter`, `input`, `fill` | Enter text into form fields | `type 'John Doe' in name field` |
| **Wait** | `wait for` | Wait for elements to appear | `wait for loading to complete` |
| **Verify** | `verify`, `check` | Verify text exists on page | `verify that 'Welcome' appears` |

### Important Notes:
- **Navigate** is only for complete URLs (http://example.com)
- **Click** is used for navigating within the site ("go to Products" = click Products link)
- **Verify** actions check if specific text appears on the current page

## Element Identification Strategies

The application uses multiple strategies to find web elements:

1. **Direct selectors**: ID, name, class name
2. **Text-based**: Button text, link text, placeholder text
3. **Label association**: Form labels linked to inputs
4. **Type-specific**: Email inputs, password fields, submit buttons
5. **Smart matching**: Partial text matches and common patterns

## Architecture

- **Models/TestAction.cs**: Defines test actions and scenarios
- **Services/PromptParser.cs**: Converts natural language to test actions
- **Services/ElementFinder.cs**: Intelligent web element identification
- **Services/TestReporter.cs**: Logging and report generation
- **Program.cs**: Main console application interface

## Test Reports

The application generates:
- Real-time console logging
- JSON test reports (`test-report.json`)
- Execution summary with pass/fail statistics
- Detailed action-by-action logs

## Configuration

The application supports various Chrome options:
- Headless mode for CI/CD environments
- Custom window sizing (default: 1920x1080)
- Stability optimizations (no-sandbox, disable-dev-shm-usage)

## Error Handling

- Graceful handling of missing elements
- Multiple fallback strategies for element identification
- Comprehensive error logging and reporting
- Automatic retry mechanisms for common issues

## Troubleshooting

### Common Issues

#### OpenAI API Issues
- **Missing API Key**: The application requires an OpenAI API key to function
- **Invalid API Key**: Check that your API key starts with `sk-` and is correctly set
- **Rate Limit Error**: The tool handles rate limits with automatic delays between requests

#### Browser Issues
- **Browser Not Found**: Playwright automatically downloads browser binaries
- **Installation Issues**: Run `pwsh bin/Debug/net9.0/playwright.ps1 install` if needed
- **Permissions**: Ensure the application can download and execute browser files

#### Element Not Found
- The tool uses multiple fallback strategies to find elements
- Try more specific or more general descriptions
- Check if the element is in a frame or shadow DOM

#### Permission Issues
- Run as administrator if needed
- Check antivirus isn't blocking browser automation

### Getting Help

For issues or questions:
1. Check the console output for detailed error messages
2. Review the generated `test-report.json` for execution details
3. Try running in headed mode to see what's happening
4. Verify your OpenAI API key is correctly set

## Project Architecture

### Core Components

- **Program.cs**: Main application entry point and user interface
- **Models/TestAction.cs**: Defines test actions and scenarios data structures
- **Services/AIPromptParser.cs**: OpenAI GPT integration for natural language parsing
- **Services/PlaywrightExecutor.cs**: Playwright automation engine for browser control
- **Services/TestReporter.cs**: Test execution logging and JSON report generation

### Dependencies

- **Microsoft.Playwright**: Modern browser automation framework
- **Newtonsoft.Json**: JSON processing for reports and API communication
- **System.Text.Json**: Built-in JSON handling
- **Microsoft.Extensions.Http**: HTTP client for OpenAI API calls

## Advanced Configuration

### Cost Management for OpenAI API

The tool includes several cost-saving features:
- **Caching**: Identical prompts are cached to avoid duplicate API calls
- **Rate Limiting**: Built-in delays between API calls to prevent rate limit charges
- **Efficient Prompts**: Optimized system prompts to minimize token usage

Estimated cost: ~$0.001-0.002 per test prompt with GPT-3.5-turbo.

### CI/CD Integration

```yaml
# Example GitHub Actions workflow
- name: Run Web Tests
  run: |
    export OPENAI_API_KEY=${{ secrets.OPENAI_API_KEY }}
    echo "navigate to https://myapp.com, verify homepage loads" | dotnet run
```

## Contributing

This tool is designed to be extensible. Key areas for contribution:
- Enhanced AI prompt engineering in `AIPromptParser.cs`
- Additional browser support in `PlaywrightExecutor.cs`
- New action types and verification methods
- Integration with popular testing frameworks
- Improved element identification strategies

## License

This project is provided as-is for educational and testing purposes.