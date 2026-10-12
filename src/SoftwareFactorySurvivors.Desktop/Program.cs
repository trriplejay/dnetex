using SoftwareFactorySurvivors.Core;

StartOptions start;
try
{
    start = StartOptions.Parse(args);
}
catch (ArgumentException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

using var game = new SoftwareFactorySurvivors.Desktop.SurvivorsGame(start);
game.Run();
return 0;
