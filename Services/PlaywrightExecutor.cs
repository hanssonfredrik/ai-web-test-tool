using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebTestAutomation.Models;

namespace WebTestAutomation.Services
{
    public class PlaywrightExecutor : IDisposable
    {
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IPage? _page;
        private bool _disposed = false;

        public event Action<string>? OnLog;

        public async Task InitializeAsync(bool headless = false)
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless,
                Args = new[] { "--disable-web-security", "--disable-features=VizDisplayCompositor" }
            });
            
            _page = await _browser.NewPageAsync();
            
            // Set viewport
            await _page.SetViewportSizeAsync(1920, 1080);
            
            Log("Playwright initialized successfully");
        }

        public async Task<bool> ExecuteScenarioAsync(TestScenario scenario)
        {
            if (_page == null)
            {
                Log("ERROR: Playwright not initialized. Call InitializeAsync() first.");
                return false;
            }

            Log($"Starting execution of scenario: {scenario.Name}");
            Log($"Description: {scenario.Description}");

            try
            {
                foreach (var action in scenario.Actions)
                {
                    if (!await ExecuteActionAsync(action))
                    {
                        Log($"ERROR: Failed to execute action: {action.Type} - {action.Target}");
                        return false;
                    }
                    
                    // Wait longer between actions to allow for page updates, menu expansions, etc.
                    await Task.Delay(1500);
                }

                Log("Scenario executed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Exception during scenario execution: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteActionAsync(TestAction action)
        {
            if (_page == null)
                return false;

            Log($"Executing {action.Type}: {action.Target} = {action.Value}");

            try
            {
                switch (action.Type)
                {
                    case ActionType.Navigate:
                        return await ExecuteNavigateAsync(action);
                    
                    case ActionType.Click:
                        return await ExecuteClickAsync(action);
                    
                    case ActionType.Type:
                        return await ExecuteTypeAsync(action);
                    
                    case ActionType.WaitForElement:
                        return await ExecuteWaitAsync(action);
                    
                    case ActionType.VerifyText:
                        return await ExecuteVerifyTextAsync(action);
                    
                    case ActionType.VerifyUrl:
                        return await ExecuteVerifyUrlAsync(action);
                    
                    default:
                        Log($"WARNING: Unknown action type: {action.Type}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: Exception executing {action.Type}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteNavigateAsync(TestAction action)
        {
            if (_page == null) return false;

            try
            {
                // Only treat as URL if it clearly looks like one
                if (action.Target.StartsWith("http") || action.Target.Contains(".") || action.Target.Contains("/"))
                {
                    var url = action.Target.StartsWith("http") ? action.Target : $"https://{action.Target}";
                    
                    // Retry navigation with different wait conditions
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            Log($"Navigation attempt {attempt}/3 to: {url}");
                            await _page.GotoAsync(url, new PageGotoOptions 
                            { 
                                WaitUntil = WaitUntilState.NetworkIdle,
                                Timeout = 30000 
                            });
                            
                            // Wait a bit more for dynamic content
                            await Task.Delay(1000);
                            Log($"Successfully navigated to: {url}");
                            return true;
                        }
                        catch (Exception ex) when (attempt < 3)
                        {
                            Log($"Navigation attempt {attempt} failed: {ex.Message}. Retrying...");
                            await Task.Delay(2000); // Wait before retry
                        }
                    }
                }
                else
                {
                    Log($"ERROR: '{action.Target}' doesn't look like a URL. Did you mean to click on a '{action.Target}' link instead?");
                    return false;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to navigate to {action.Target}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteClickAsync(TestAction action)
        {
            if (_page == null) return false;

            Log($"Looking for clickable element: {action.Target}");

            try
            {
                // Playwright's smart selectors - try multiple strategies
                ILocator? element = null;
                
                // Clean up the target - remove common suffixes
                var cleanTarget = CleanElementTarget(action.Target);
                Log($"Cleaned target: '{action.Target}' → '{cleanTarget}'");

                // Strategy 1: Exact button/link text with cleaned target
                var buttonByText = _page.GetByRole(AriaRole.Button, new() { Name = cleanTarget });
                if (await buttonByText.CountAsync() > 0)
                {
                    element = buttonByText;
                    Log($"Found button with exact text: {cleanTarget}");
                }

                // Strategy 1b: Try original target if clean didn't work
                if (element == null && cleanTarget != action.Target)
                {
                    buttonByText = _page.GetByRole(AriaRole.Button, new() { Name = action.Target });
                    if (await buttonByText.CountAsync() > 0)
                    {
                        element = buttonByText;
                        Log($"Found button with exact text: {action.Target}");
                    }
                }

                // Strategy 2: Link with cleaned text
                if (element == null)
                {
                    var linkByText = _page.GetByRole(AriaRole.Link, new() { Name = cleanTarget });
                    if (await linkByText.CountAsync() > 0)
                    {
                        element = linkByText;
                        Log($"Found link with exact text: {cleanTarget}");
                    }
                }

                // Strategy 2b: Link with original text
                if (element == null && cleanTarget != action.Target)
                {
                    var linkByText = _page.GetByRole(AriaRole.Link, new() { Name = action.Target });
                    if (await linkByText.CountAsync() > 0)
                    {
                        element = linkByText;
                        Log($"Found link with exact text: {action.Target}");
                    }
                }

                // Strategy 3: Any clickable element with the cleaned text (handle multiple matches)
                if (element == null)
                {
                    var anyByCleanText = _page.GetByText(cleanTarget);
                    var count = await anyByCleanText.CountAsync();
                    if (count > 0)
                    {
                        if (count == 1)
                        {
                            element = anyByCleanText;
                            Log($"Found element with cleaned text: {cleanTarget}");
                        }
                        else
                        {
                            // Multiple matches - try to find the most clickable one
                            Log($"Found {count} elements with text '{cleanTarget}', selecting the most appropriate one");
                            
                            // Try to find one that's actually clickable (link or button)
                            for (int i = 0; i < count; i++)
                            {
                                var candidateElement = anyByCleanText.Nth(i);
                                var tagName = await candidateElement.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
                                var role = await candidateElement.GetAttributeAsync("role");
                                
                                // Prefer actual links and buttons
                                if (tagName == "a" || tagName == "button" || role == "button" || role == "link")
                                {
                                    element = candidateElement;
                                    Log($"Selected {tagName} element with text: {cleanTarget}");
                                    break;
                                }
                            }
                            
                            // If no clear clickable element, take the first one
                            if (element == null)
                            {
                                element = anyByCleanText.Nth(0);
                                Log($"Selected first element with text: {cleanTarget}");
                            }
                        }
                    }
                }

                // Strategy 3b: Any element with original text (handle multiple matches)
                if (element == null && cleanTarget != action.Target)
                {
                    var anyByText = _page.GetByText(action.Target);
                    var count = await anyByText.CountAsync();
                    if (count > 0)
                    {
                        if (count == 1)
                        {
                            element = anyByText;
                            Log($"Found element with text: {action.Target}");
                        }
                        else
                        {
                            Log($"Found {count} elements with text '{action.Target}', selecting the most appropriate one");
                            
                            // Try to find one that's actually clickable
                            for (int i = 0; i < count; i++)
                            {
                                var candidateElement = anyByText.Nth(i);
                                var tagName = await candidateElement.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
                                var role = await candidateElement.GetAttributeAsync("role");
                                
                                if (tagName == "a" || tagName == "button" || role == "button" || role == "link")
                                {
                                    element = candidateElement;
                                    Log($"Selected {tagName} element with text: {action.Target}");
                                    break;
                                }
                            }
                            
                            if (element == null)
                            {
                                element = anyByText.Nth(0);
                                Log($"Selected first element with text: {action.Target}");
                            }
                        }
                    }
                }

                // Strategy 4: Partial text match for buttons with cleaned target
                if (element == null)
                {
                    var buttonByPartialText = _page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex($".*{System.Text.RegularExpressions.Regex.Escape(cleanTarget)}.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
                    if (await buttonByPartialText.CountAsync() > 0)
                    {
                        element = buttonByPartialText;
                        Log($"Found button with partial text: {cleanTarget}");
                    }
                }

                // Strategy 5: Partial text match for links with cleaned target
                if (element == null)
                {
                    var linkByPartialText = _page.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex($".*{System.Text.RegularExpressions.Regex.Escape(cleanTarget)}.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
                    if (await linkByPartialText.CountAsync() > 0)
                    {
                        element = linkByPartialText;
                        Log($"Found link with partial text: {cleanTarget}");
                    }
                }

                // Strategy 6: Partial text match for buttons with original target (fallback)
                if (element == null && cleanTarget != action.Target)
                {
                    var buttonByPartialText = _page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex($".*{System.Text.RegularExpressions.Regex.Escape(action.Target)}.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
                    if (await buttonByPartialText.CountAsync() > 0)
                    {
                        element = buttonByPartialText;
                        Log($"Found button with partial text: {action.Target}");
                    }
                }

                // Strategy 7: Partial text match for links with original target (fallback)
                if (element == null && cleanTarget != action.Target)
                {
                    var linkByPartialText = _page.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex($".*{System.Text.RegularExpressions.Regex.Escape(action.Target)}.*", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
                    if (await linkByPartialText.CountAsync() > 0)
                    {
                        element = linkByPartialText;
                        Log($"Found link with partial text: {action.Target}");
                    }
                }

                if (element == null)
                {
                    Log($"ERROR: Could not find any clickable element with text: {action.Target}");
                    
                    // Show available clickable elements for debugging
                    await LogAvailableElementsAsync();
                    return false;
                }

                // Click with automatic waiting and retries
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        await element.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
                        Log($"Successfully clicked: {action.Target}");
                        
                        // Wait longer after click for navigation/loading/menu expansion
                        await Task.Delay(1000);
                        
                        // If this looks like a navigation click, wait for page to stabilize
                        if (action.Target.ToLower().Contains("product") || 
                            action.Target.ToLower().Contains("dashboard") ||
                            action.Target.ToLower().Contains("menu") ||
                            action.Target.ToLower().Contains("nav"))
                        {
                            Log("Navigation click detected - waiting for page/menu to stabilize");
                            await Task.Delay(2000);
                            
                            // Wait for network activity to settle
                            try
                            {
                                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 5000 });
                            }
                            catch (TimeoutException)
                            {
                                // Continue if network doesn't idle quickly
                                Log("Network didn't idle, continuing anyway");
                            }
                        }
                        
                        return true;
                    }
                    catch (Exception ex) when (attempt < 3)
                    {
                        Log($"Click attempt {attempt} failed: {ex.Message}. Retrying...");
                        await Task.Delay(1000);
                    }
                }
                return false;
            }
            catch (TimeoutException)
            {
                Log($"ERROR: Timeout waiting for element to be clickable: {action.Target}");
                await LogAvailableElementsAsync();
                return false;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to click element {action.Target}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteTypeAsync(TestAction action)
        {
            if (_page == null) return false;

            try
            {
                ILocator? element = null;

                // Strategy 1: Input by label text
                var inputByLabel = _page.GetByLabel(action.Target);
                if (await inputByLabel.CountAsync() > 0)
                {
                    element = inputByLabel;
                    Log($"Found input by label: {action.Target}");
                }

                // Strategy 2: Input by placeholder
                if (element == null)
                {
                    var inputByPlaceholder = _page.GetByPlaceholder(action.Target);
                    if (await inputByPlaceholder.CountAsync() > 0)
                    {
                        element = inputByPlaceholder;
                        Log($"Found input by placeholder: {action.Target}");
                    }
                }

                // Strategy 3: Input by type (email, password)
                if (element == null && action.Target.ToLower().Contains("email"))
                {
                    var emailInput = _page.Locator("input[type='email']");
                    if (await emailInput.CountAsync() > 0)
                    {
                        element = emailInput;
                        Log("Found email input by type");
                    }
                }

                if (element == null && action.Target.ToLower().Contains("password"))
                {
                    var passwordInput = _page.Locator("input[type='password']");
                    if (await passwordInput.CountAsync() > 0)
                    {
                        element = passwordInput;
                        Log("Found password input by type");
                    }
                }

                // Strategy 4: Generic text input
                if (element == null)
                {
                    var textInput = _page.Locator($"input[name*='{action.Target}'], input[id*='{action.Target}']");
                    if (await textInput.CountAsync() > 0)
                    {
                        element = textInput;
                        Log($"Found input by name/id containing: {action.Target}");
                    }
                }

                if (element == null)
                {
                    Log($"ERROR: Could not find input field: {action.Target}");
                    return false;
                }

                // Clear and type with automatic waiting
                await element.ClearAsync();
                await element.FillAsync(action.Value);
                Log($"Typed '{action.Value}' into: {action.Target}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to type into {action.Target}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteWaitAsync(TestAction action)
        {
            if (_page == null) return false;

            try
            {
                await _page.GetByText(action.Target).WaitForAsync(new LocatorWaitForOptions { Timeout = action.TimeoutSeconds * 1000 });
                Log($"Successfully waited for element: {action.Target}");
                return true;
            }
            catch (TimeoutException)
            {
                Log($"Timeout waiting for element: {action.Target}");
                return false;
            }
        }

        private async Task<bool> ExecuteVerifyTextAsync(TestAction action)
        {
            if (_page == null) return false;

            try
            {
                if (string.IsNullOrWhiteSpace(action.Value))
                {
                    Log($"ERROR: VerifyText action has empty value. Expected text to verify is missing.");
                    Log($"Action details - Target: '{action.Target}', Value: '{action.Value}'");
                    return false;
                }

                var textToVerify = action.Value.Trim();
                Log($"Verifying text is visible: '{textToVerify}'");
                
                var element = _page.GetByText(textToVerify);
                var count = await element.CountAsync();
                
                if (count == 0)
                {
                    Log($"Verify text '{textToVerify}': Not found");
                    return false;
                }
                else if (count == 1)
                {
                    var isVisible = await element.IsVisibleAsync();
                    Log($"Verify text '{textToVerify}': {(isVisible ? "Found and visible" : "Found but not visible")}");
                    return isVisible;
                }
                else
                {
                    // Multiple matches - check if any are visible
                    Log($"Found {count} elements with text '{textToVerify}', checking visibility");
                    for (int i = 0; i < count; i++)
                    {
                        var candidateElement = element.Nth(i);
                        var isVisible = await candidateElement.IsVisibleAsync();
                        if (isVisible)
                        {
                            Log($"Verify text '{textToVerify}': Found and visible (match {i + 1} of {count})");
                            return true;
                        }
                    }
                    Log($"Verify text '{textToVerify}': Found {count} matches but none are visible");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to verify text '{action.Value}': {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteVerifyUrlAsync(TestAction action)
        {
            if (_page == null) return false;

            try
            {
                var currentUrl = _page.Url;
                var matches = currentUrl.Contains(action.Value);
                Log($"Verify URL contains '{action.Value}': {(matches ? "Match" : "No match")} (Current: {currentUrl})");
                return matches;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to verify URL: {ex.Message}");
                return false;
            }
        }

        private async Task LogAvailableElementsAsync()
        {
            if (_page == null) return;

            try
            {
                Log("Available clickable elements on page:");
                
                // List buttons
                var buttons = await _page.GetByRole(AriaRole.Button).AllAsync();
                foreach (var button in buttons.Take(5))
                {
                    var text = await button.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                        Log($"  Button: '{text.Trim()}'");
                }

                // List links
                var links = await _page.GetByRole(AriaRole.Link).AllAsync();
                foreach (var link in links.Take(5))
                {
                    var text = await link.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                        Log($"  Link: '{text.Trim()}'");
                }
            }
            catch
            {
                // Ignore errors in debugging info
            }
        }

        private string CleanElementTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return target;

            var cleaned = target.Trim();
            
            // Remove common suffixes that users might add but aren't in the actual element text
            var suffixesToRemove = new[] { " button", " link", " field", " input", " text", " box", " element" };
            
            foreach (var suffix in suffixesToRemove)
            {
                if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - suffix.Length).Trim();
                    break; // Only remove one suffix
                }
            }
            
            return cleaned;
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            Console.WriteLine(logMessage);
            OnLog?.Invoke(logMessage);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _browser?.CloseAsync().Wait();
                _playwright?.Dispose();
                _disposed = true;
                Log("Playwright disposed");
            }
        }
    }
}