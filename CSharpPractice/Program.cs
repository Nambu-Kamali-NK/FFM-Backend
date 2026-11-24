using System;
using System.Collections.Generic;

namespace CSharpPractice;

class Program
{
    public static void Main(string[] args)
    {
        string logMessage = "Low code no code are important";
        var result = countWord(logMessage);
        foreach (var item in result)
        {
            Console.WriteLine($"{item.Key} : {item.Value}");
        }
    }
    public static Dictionary<string, int> countWord(string logMessage)
    {
        var word = new Dictionary<string, int>();
        string[] words = logMessage.Split(' ');
        foreach (string w in words)
        {
            if (word.ContainsKey(w))
            {
                word[w]++;
            }
            else
            {
                word[w] = 1;
            }
        }

        return word;
    }
}
