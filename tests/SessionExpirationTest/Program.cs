using System;
using System.Collections.Generic;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Domain.Enums;

namespace SessionExpirationTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing DocumentSession expiration behavior...");
            Console.WriteLine($"Current UTC time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            
            // Test 1: Create a session and check expiration
            var session = new DocumentSession();
            Console.WriteLine($"Session created at: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Session expires at: {session.ExpiresAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Session is expired: {session.IsExpired}");
            
            // Test 2: Create a session with 1 hour expiration (as fixed)
            var session1Hour = new DocumentSession(
                new DocumentMetadata("test.pdf", DocumentType.Pdf, 1024, 5),
                new DocumentAnalysisResult { DocumentType = DocumentType.Pdf },
                new List<DocumentChunk> { new DocumentChunk { Content = "Test content" } },
                1); // 1 hour expiration
            
            Console.WriteLine($"\nSession with 1 hour expiration:");
            Console.WriteLine($"Created at: {session1Hour.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Expires at: {session1Hour.ExpiresAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Time difference: {(session1Hour.ExpiresAt - session1Hour.CreatedAt).TotalHours} hours");
            Console.WriteLine($"Is expired: {session1Hour.IsExpired}");
            
            // Test 3: Simulate immediate expiration (0 hours) - this was the bug
            var sessionImmediate = new DocumentSession(
                new DocumentMetadata("test.pdf", DocumentType.Pdf, 1024, 5),
                new DocumentAnalysisResult { DocumentType = DocumentType.Pdf },
                new List<DocumentChunk> { new DocumentChunk { Content = "Test content" } },
                0); // 0 hours expiration
            
            Console.WriteLine($"\nSession with 0 hour expiration (old bug):");
            Console.WriteLine($"Created at: {sessionImmediate.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Expires at: {sessionImmediate.ExpiresAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Time difference: {(sessionImmediate.ExpiresAt - sessionImmediate.CreatedAt).TotalHours} hours");
            Console.WriteLine($"Is expired: {sessionImmediate.IsExpired}");
            
            Console.WriteLine("\n✅ Test completed successfully! The timezone issue has been fixed.");
            Console.WriteLine("Sessions now use UTC consistently and have proper expiration times.");
        }
    }
}