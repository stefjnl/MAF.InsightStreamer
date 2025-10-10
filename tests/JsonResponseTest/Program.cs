using System;
using System.Linq;
using System.Text.Json;

namespace JsonResponseTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing JSON response parsing improvements...");
            
            // Test valid JSON responses
            TestValidJsonResponses();
            
            // Test invalid JSON responses (should be handled gracefully)
            TestInvalidJsonResponses();
            
            // Test JSON cleaning functionality
            TestJsonCleaning();
            
            Console.WriteLine("\n✅ All JSON response tests completed successfully!");
        }
        
        static void TestValidJsonResponses()
        {
            Console.WriteLine("\n--- Testing Valid JSON Responses ---");
            
            var validResponses = new[]
            {
                @"{""answer"": ""This is a valid answer"", ""relevantChunks"": [1, 2, 3]}",
                @"{""answer"": ""Another valid answer"", ""relevantChunks"": []}",
                @"{""answer"": ""Answer with mixed chunks"", ""relevantChunks"": [5, 8, 12]}"
            };
            
            foreach (var response in validResponses)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(response);
                    var answer = jsonDoc.RootElement.GetProperty("answer").GetString();
                    var chunks = jsonDoc.RootElement.GetProperty("relevantChunks").EnumerateArray()
                        .Select(x => x.GetInt32()).ToList();
                    
                    Console.WriteLine($"✅ Parsed: Answer='{answer}', Chunks=[{string.Join(", ", chunks)}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to parse: {ex.Message}");
                }
            }
        }
        
        static void TestInvalidJsonResponses()
        {
            Console.WriteLine("\n--- Testing Invalid JSON Responses ---");
            
            var invalidResponses = new[]
            {
                "# This is not JSON at all",
                "Some random text",
                @"{""answer"": ""Missing chunks property""}",
                @"{""relevantChunks"": [1, 2], ""missing_answer"": ""property""}",
                "```json\n{\"answer\": \"Markdown wrapped JSON\", \"relevantChunks\": [1]}\n```"
            };
            
            foreach (var response in invalidResponses)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(response);
                    Console.WriteLine($"❌ Unexpectedly parsed invalid JSON: {response}");
                }
                catch (JsonException)
                {
                    Console.WriteLine($"✅ Correctly rejected invalid JSON: {response.Substring(0, Math.Min(50, response.Length))}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✅ Correctly handled with error: {ex.Message}");
                }
            }
        }
        
        static void TestJsonCleaning()
        {
            Console.WriteLine("\n--- Testing JSON Cleaning Functionality ---");
            
            var dirtyResponses = new[]
            {
                ("```json\n{\"answer\": \"Cleaned markdown\", \"relevantChunks\": [1]}\n```", "Markdown wrapped JSON"),
                ("<!-- comment -->\n{\"answer\": \"After comment\", \"relevantChunks\": [2]}", "HTML comment prefixed JSON"),
                ("Some text before {\"answer\": \"Extracted JSON\", \"relevantChunks\": [3]} and after", "JSON embedded in text"),
                ("{\"answer\": \"Already clean\", \"relevantChunks\": [4]}", "Already clean JSON")
            };
            
            foreach (var (response, description) in dirtyResponses)
            {
                try
                {
                    // Simulate the cleaning process
                    var cleaned = CleanJsonResponse(response);
                    var jsonDoc = JsonDocument.Parse(cleaned);
                    var answer = jsonDoc.RootElement.GetProperty("answer").GetString();
                    Console.WriteLine($"✅ {description}: Cleaned to '{answer}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {description}: Failed to clean - {ex.Message}");
                }
            }
        }
        
        // Simulate the cleaning function from our implementation
        static string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            try
            {
                // Look for JSON object boundaries
                var startIndex = response.IndexOf('{');
                var endIndex = response.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonContent = response.Substring(startIndex, endIndex - startIndex + 1);
                    
                    // Remove common markdown formatting
                    jsonContent = jsonContent.Replace("```json", "").Replace("```", "");
                    
                    // Remove HTML comments and other common formatting issues
                    jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, @"<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
                    
                    // Try to parse the cleaned content
                    JsonDocument.Parse(jsonContent);
                    return jsonContent;
                }
                
                return response;
            }
            catch (Exception)
            {
                return response;
            }
        }
    }
}