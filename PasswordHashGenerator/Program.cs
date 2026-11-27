using System;

class Program
{
    static void Main()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("AdminPassword@123");
        Console.WriteLine("BCRYPT_HASH: " + hash);
    }
}
