using System;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string hash = "$2a$11$GMmgteVt27GjZjbkvnuhDO6FYRtj/wjeqxGUjTJ/2ihUGQEXlbiF6";
        var list = new List<string>
        {
            "Kamali@123", "kamali@123", "Kamali123", "Kamali@12", "Kamali#123",
            "Nambu@123", "nambu@123", "NambuKamali@123", "Nambu@1234",
            "Admin@123", "admin@123", "Admin@1234", "Admin123", "Admin!23",
            "Test@123", "test@123", "Test@1234", "Testing@123", "TestUser@123",
            "Password@123", "password@123", "Password123", "P@ssw0rd123", "P@ssword1",
            "SuperSecretDevelopmentKey123!", "ffm_pass", "ffmid_user",
            "12345678", "123456789", "Welcome@123", "Secret123!", "Secret@123"
        };

        foreach (var p in list)
        {
            if (BCrypt.Net.BCrypt.Verify(p, hash))
            {
                Console.WriteLine("FOUND_PASSWORD_MATCH: " + p);
                return;
            }
        }
        Console.WriteLine("NO_MATCH_IN_LIST");
    }
}
