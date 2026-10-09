using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

const int StartingGold = 120;
const int StartingLives = 10;

var game = new TowerDefenseGame();
game.Run();

sealed class TowerDefenseGame
{
    private const int MapWidth = 32;
    private const int MapHeight = 14;
    private const int TowerCost = 45;
    private const int UpgradeCost = 35;

    private readonly char[,] _map = new char[MapHeight, MapWidth];
    private readonly List<Tower> _towers = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<(int X, int Y)> _path = new();

    private int _gold = StartingGold;
    private int _lives = StartingLives;
    private int _wave;
    private int _score;

    public void Run()
    {
        BuildMap();

        while (_lives > 0)
        {
            Draw();

            Console.WriteLine();
            Console.WriteLine("1) Start next wave");
            Console.WriteLine($"2) Build tower ({TowerCost} gold)");
            Console.WriteLine($"3) Upgrade tower ({UpgradeCost} gold)");
            Console.WriteLine("4) Help");
            Console.WriteLine("5) Quit");
            Console.Write("Choose an action: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    RunWave();
                    break;
                case "2":
                    BuildTower();
                    break;
                case "3":
                    UpgradeTower();
                    break;
                case "4":
                    ShowHelp();
                    break;
                case "5":
                    Console.WriteLine("Thanks for playing!");
                    return;
                default:
                    Console.WriteLine("Choose a number from 1 to 5.");
                    Pause();
                    break;
            }
        }

        Draw();
        Console.WriteLine($"Game over! Final score: {_score}");
    }

    private void BuildMap()
    {
        // Winding path from the left edge to the right edge.
        for (int x = 0; x <= 8; x++) _path.Add((x, 3));
        for (int y = 3; y <= 9; y++) _path.Add((8, y));
        for (int x = 8; x <= 20; x++) _path.Add((x, 9));
        for (int y = 5; y <= 9; y++) _path.Add((20, y));
        for (int x = 20; x < MapWidth; x++) _path.Add((x, 5));

        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
            _map[y, x] = '.';

        foreach (var (x, y) in _path)
            _map[y, x] = '#';

        _map[_path[0].Y, _path[0].X] = 'S';
        var end = _path[^1];
        _map[end.Y, end.X] = 'E';
    }

    private void Draw()
    {
        Console.Clear();
        Console.WriteLine("=== YOU: TOWER DEFENSE ===");
        Console.WriteLine($"Wave: {_wave}   Gold: {_gold}   Lives: {_lives}   Score: {_score}");
        Console.WriteLine();

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                char cell = _map[y, x];

                if (_enemies.Any(e => e.X == x && e.Y == y))
                    cell = 'e';
                else if (_towers.Any(t => t.X == x && t.Y == y))
                    cell = 'T';

                Console.Write(cell);
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("Legend: # path  S start  E exit  T tower  e enemy  . buildable ground");
    }

    private void BuildTower()
    {
        if (_gold < TowerCost)
        {
            Message("Not enough gold.");
            return;
        }

        Console.Write($"Tower location (x y), from 0 to {MapWidth - 1}, 0 to {MapHeight - 1}: ");
        if (!TryReadCoordinates(out int x, out int y))
        {
            Message("Invalid coordinates.");
            return;
        }

        if (_map[y, x] != '.' || _towers.Any(t => t.X == x && t.Y == y))
        {
            Message("Choose an empty ground tile, not the path.");
            return;
        }

        _gold -= TowerCost;
        _towers.Add(new Tower(x, y));
        Message("Tower built!");
    }

    private void UpgradeTower()
    {
        if (_gold < UpgradeCost)
        {
            Message("Not enough gold.");
            return;
        }

        if (_towers.Count == 0)
        {
            Message("Build a tower first.");
            return;
        }

        Console.Write("Tower location to upgrade (x y): ");
        if (!TryReadCoordinates(out int x, out int y))
        {
            Message("Invalid coordinates.");
            return;
        }

        Tower? tower = _towers.FirstOrDefault(t => t.X == x && t.Y == y);
        if (tower is null)
        {
            Message("There is no tower at that location.");
            return;
        }

        _gold -= UpgradeCost;
        tower.Level++;
        tower.Range++;
        tower.Damage++;
        Message($"Tower upgraded to level {tower.Level}!");
    }

    private bool TryReadCoordinates(out int x, out int y)
    {
        string[] parts = (Console.ReadLine() ?? "").Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
        );

        return parts.Length == 2
            && int.TryParse(parts[0], out x)
            && int.TryParse(parts[1], out y)
            && x >= 0 && x < MapWidth
            && y >= 0 && y < MapHeight;
    }

    private void RunWave()
    {
        _wave++;
        int enemyCount = 5 + _wave * 2;

        Console.WriteLine($"Wave {_wave} incoming! {enemyCount} enemies.");
        Thread.Sleep(900);

        for (int i = 0; i < enemyCount; i++)
        {
            _enemies.Add(new Enemy(_wave));
            Tick();
            Thread.Sleep(250);
        }

        while (_enemies.Count > 0 && _lives > 0)
        {
            Tick();
            Thread.Sleep(250);
        }

        if (_lives > 0)
        {
            int bonus = 10 + _wave * 2;
            _gold += bonus;
            Console.WriteLine($"Wave cleared! Wave bonus: {bonus} gold.");
            Pause();
        }
    }

    private void Tick()
    {
        // Towers attack the closest enemy within range.
        foreach (Tower tower in _towers)
        {
            Enemy? target = _enemies
                .Where(e => Distance(tower.X, tower.Y, e.X, e.Y) <= tower.Range)
                .OrderByDescending(e => e.PathIndex)
                .FirstOrDefault();

            if (target is not null)
            {
                target.Health -= tower.Damage;

                if (target.Health <= 0)
                {
                    _enemies.Remove(target);
                    _gold += 8;
                    _score += 10;
                    break;
                }
            }
        }

        // Move enemies forward along the path.
        foreach (Enemy enemy in _enemies.ToList())
        {
            enemy.MoveCounter++;

            if (enemy.MoveCounter < enemy.Speed)
                continue;

            enemy.MoveCounter = 0;
            enemy.PathIndex++;

            if (enemy.PathIndex >= _path.Count)
            {
                _enemies.Remove(enemy);
                _lives--;
            }
            else
            {
                (enemy.X, enemy.Y) = _path[enemy.PathIndex];
            }
        }

        Draw();
    }

    private static double Distance(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Defend the exit from enemies traveling along the # path.");
        Console.WriteLine($"Build towers for {TowerCost} gold, then upgrade them for {UpgradeCost} gold.");
        Console.WriteLine("Towers automatically attack enemies in range.");
        Console.WriteLine("You earn gold for defeating enemies and clearing waves.");
        Console.WriteLine("If an enemy reaches E, you lose a life.");
        Pause();
    }

    private static void Message(string text)
    {
        Console.WriteLine(text);
        Pause();
    }

    private static void Pause()
    {
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}

sealed class Tower(int x, int y)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Level { get; set; } = 1;
    public int Range { get; set; } = 4;
    public int Damage { get; set; } = 1;
}

sealed class Enemy(int wave)
{
    public int PathIndex { get; set; }
    public int X { get; set; } = 0;
    public int Y { get; set; } = 3;
    public int Health { get; set; } = 3 + wave;
    public int Speed { get; } = Math.Max(1, 4 - wave / 3);
    public int MoveCounter { get; set; }
}
