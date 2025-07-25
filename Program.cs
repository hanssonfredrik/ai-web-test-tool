using System;
using System.Net.Http;
using WebTestAutomation.Services;

namespace WebTestAutomation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Web Test Automation Tool ===");
            Console.WriteLine("Enter natural language prompts to automate web testing.");
            Console.WriteLine("Example: 'login to the application using john.doe@emaik.com with password mypassword, then go to products and create a product'");
            Console.WriteLine("Type 'exit' to quit.");
            
            var httpClient = new HttpClient();
            
            try
            {
                var aiParser = new AIPromptParser(httpClient);
                var reporter = new TestReporter();
                Console.WriteLine("✓ AI-powered parsing enabled (OpenAI API key found)");
                Console.WriteLine();

                Console.Write("Initialize browser in headless mode? (y/n): ");
                var headlessInput = Console.ReadLine()?.ToLower();
                var headless = headlessInput == "y" || headlessInput == "yes";

                Console.WriteLine("\n🎭 Using Playwright for browser automation");
                using var executor = new PlaywrightExecutor();
                executor.OnLog += reporter.AddLog;

                try
                {
                    await executor.InitializeAsync(headless);
                    await RunTestLoop(aiParser, reporter, executor);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                }
                finally
                {
                    reporter.GenerateReport();
                    reporter.PrintSummary();
                    Console.WriteLine("\nPress any key to exit...");
                    try
                    {
                        Console.ReadKey();
                    }
                    catch (InvalidOperationException)
                    {
                        // Handle redirected input gracefully
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                Console.WriteLine("\nTo fix this:");
                Console.WriteLine("1. Get an OpenAI API key from https://platform.openai.com/api-keys");
                Console.WriteLine("2. Set the environment variable:");
                Console.WriteLine("   Windows CMD: set OPENAI_API_KEY=sk-your-key-here");
                Console.WriteLine("   Windows PowerShell: $env:OPENAI_API_KEY=\"sk-your-key-here\"");
                Console.WriteLine("   Linux/macOS: export OPENAI_API_KEY=sk-your-key-here");
                Console.WriteLine("\nPress any key to exit...");
                try
                {
                    Console.ReadKey();
                }
                catch (InvalidOperationException)
                {
                    // Handle redirected input gracefully
                }
            }
        }

        private static async Task RunTestLoop(AIPromptParser aiParser, TestReporter reporter, PlaywrightExecutor executor)
        {
            while (true)
            {
                Console.Write("\nEnter your test prompt (or 'exit'): ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
                    break;

                var scenario = await aiParser.ParsePromptAsync(input);
                
                Console.WriteLine($"\nParsed scenario: {scenario.Name}");
                Console.WriteLine($"Actions to execute:");
                for (int i = 0; i < scenario.Actions.Count; i++)
                {
                    var action = scenario.Actions[i];
                    Console.WriteLine($"  {i + 1}. {action.Type}: {action.Target} = {action.Value}");
                }

                Console.Write("\nProceed with execution? (y/n): ");
                var proceed = Console.ReadLine()?.ToLower();
                
                if (proceed != "y" && proceed != "yes")
                    continue;

                reporter.StartScenario(scenario);
                var success = await executor.ExecuteScenarioAsync(scenario);
                reporter.EndScenario(success, success ? null : "Scenario execution failed");

                Console.WriteLine($"\nScenario execution: {(success ? "SUCCESS" : "FAILED")}");
            }
        }


    }
}
