using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WebTestAutomation.Models;

namespace WebTestAutomation.Services
{
    public class TestResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<string> Logs { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }
    }

    public class TestReporter
    {
        private readonly List<TestResult> _results = new List<TestResult>();
        private TestResult? _currentResult;

        public void StartScenario(TestScenario scenario)
        {
            _currentResult = new TestResult
            {
                ScenarioName = scenario.Name,
                Description = scenario.Description,
                StartTime = DateTime.Now,
                Logs = new List<string>()
            };
        }

        public void EndScenario(bool success, string? errorMessage = null)
        {
            if (_currentResult != null)
            {
                _currentResult.EndTime = DateTime.Now;
                _currentResult.Success = success;
                _currentResult.ErrorMessage = errorMessage;
                _results.Add(_currentResult);
                _currentResult = null;
            }
        }

        public void AddLog(string message)
        {
            _currentResult?.Logs.Add(message);
        }

        public void GenerateReport(string outputPath = "test-report.json")
        {
            var report = new
            {
                GeneratedAt = DateTime.Now,
                TotalScenarios = _results.Count,
                PassedScenarios = _results.Count(r => r.Success),
                FailedScenarios = _results.Count(r => !r.Success),
                Results = _results
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            File.WriteAllText(outputPath, json);
            Console.WriteLine($"Test report generated: {outputPath}");
        }

        public void PrintSummary()
        {
            Console.WriteLine("\n=== TEST EXECUTION SUMMARY ===");
            Console.WriteLine($"Total Scenarios: {_results.Count}");
            Console.WriteLine($"Passed: {_results.Count(r => r.Success)}");
            Console.WriteLine($"Failed: {_results.Count(r => !r.Success)}");
            
            if (_results.Any())
            {
                Console.WriteLine($"Average Duration: {TimeSpan.FromMilliseconds(_results.Average(r => r.Duration.TotalMilliseconds)):mm\\:ss}");
            }

            Console.WriteLine("\n=== DETAILED RESULTS ===");
            foreach (var result in _results)
            {
                Console.WriteLine($"\n{result.ScenarioName}: {(result.Success ? "PASSED" : "FAILED")}");
                Console.WriteLine($"Duration: {result.Duration:mm\\:ss}");
                
                if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"Error: {result.ErrorMessage}");
                }
            }
        }
    }
}