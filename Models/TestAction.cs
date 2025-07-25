using System.Collections.Generic;

namespace WebTestAutomation.Models
{
    public enum ActionType
    {
        Navigate,
        Click,
        Type,
        WaitForElement,
        VerifyText,
        VerifyUrl
    }

    public class TestAction
    {
        public ActionType Type { get; set; }
        public string Target { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class TestScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<TestAction> Actions { get; set; } = new List<TestAction>();
        public string BaseUrl { get; set; } = string.Empty;
    }
}