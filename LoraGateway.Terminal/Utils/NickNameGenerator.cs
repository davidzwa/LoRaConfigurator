namespace LoraGateway.Utils;

public class NameGenerator
{
    public static string GenerateName(int len)
    {
        var r = new Random();
        string[] consonants =
        {
            "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w",
            "x"
        };
        string[] vowels = {"a", "e", "i", "o", "u", "ae", "y"};
        var name = "";
        name += consonants[r.Next(consonants.Length)].ToUpper();
        name += vowels[r.Next(vowels.Length)];
        var
            b = 2; //b tells how many times a new letter has been added. It's 2 right now because the first two letters are already in the name.
        while (b < len)
        {
            name += consonants[r.Next(consonants.Length)];
            b++;
            name += vowels[r.Next(vowels.Length)];
            b++;
        }

        return name;
    }
}