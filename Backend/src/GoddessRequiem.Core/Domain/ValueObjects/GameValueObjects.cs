using System;

namespace GoddessRequiem.Core.Domain.ValueObjects
{
    public readonly record struct CurrencyAmount
    {
        public CurrencyType Type { get; }
        public long Amount { get; }

        public CurrencyAmount(CurrencyType type, long amount)
        {
            if (amount < 0)
                throw new ArgumentException("Currency amount cannot be negative", nameof(amount));

            Type = type;
            Amount = amount;
        }

        public static CurrencyAmount operator +(CurrencyAmount left, CurrencyAmount right)
        {
            if (left.Type != right.Type)
                throw new InvalidOperationException("Cannot add different currency types");

            return new CurrencyAmount(left.Type, left.Amount + right.Amount);
        }

        public static CurrencyAmount operator -(CurrencyAmount left, CurrencyAmount right)
        {
            if (left.Type != right.Type)
                throw new InvalidOperationException("Cannot subtract different currency types");

            if (left.Amount < right.Amount)
                throw new InvalidOperationException("Insufficient currency");

            return new CurrencyAmount(left.Type, left.Amount - right.Amount);
        }

        public bool CanAfford(CurrencyAmount cost) => Type == cost.Type && Amount >= cost.Amount;

        public override string ToString() => $"{Amount} {Type}";
    }

    public readonly record struct ResourceQuantity
    {
        public Guid ResourceId { get; }
        public int Quantity { get; }

        public ResourceQuantity(Guid resourceId, int quantity)
        {
            if (quantity < 0)
                throw new ArgumentException("Quantity cannot be negative", nameof(quantity));

            ResourceId = resourceId;
            Quantity = quantity;
        }

        public static ResourceQuantity operator +(ResourceQuantity left, ResourceQuantity right)
        {
            if (left.ResourceId != right.ResourceId)
                throw new InvalidOperationException("Cannot add different resources");

            return new ResourceQuantity(left.ResourceId, left.Quantity + right.Quantity);
        }
    }

    public readonly record struct Position
    {
        public int X { get; }
        public int Y { get; }

        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Position Origin => new(0, 0);

        public double DistanceTo(Position other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    }

    public readonly record struct Percentage
    {
        public float Value { get; }

        public Percentage(float value)
        {
            if (value < 0 || value > 1)
                throw new ArgumentException("Percentage must be between 0 and 1", nameof(value));

            Value = value;
        }

        public static implicit operator float(Percentage p) => p.Value;
        public static implicit operator Percentage(float f) => new(f);

        public override string ToString() => $"{Value:P2}";
    }

    public readonly record struct Level
    {
        public int Value { get; }
        public int MaxLevel { get; }

        public Level(int value, int maxLevel = 100)
        {
            if (value < 1)
                throw new ArgumentException("Level must be at least 1", nameof(value));
            if (value > maxLevel)
                throw new ArgumentException($"Level cannot exceed max level {maxLevel}", nameof(value));
            if (maxLevel < 1)
                throw new ArgumentException("Max level must be at least 1", nameof(maxLevel));

            Value = value;
            MaxLevel = maxLevel;
        }

        public bool IsMax => Value >= MaxLevel;

        public Level Up(int levels = 1) => new(Math.Min(Value + levels, MaxLevel), MaxLevel);

        public static implicit operator int(Level l) => l.Value;
        public static implicit operator Level(int i) => new(i);

        public override string ToString() => $"{Value}/{MaxLevel}";
    }

    public readonly record struct AscensionLevel
    {
        public int Value { get; }
        public int MaxAscension { get; }

        public AscensionLevel(int value, int maxAscension = 6)
        {
            if (value < 0)
                throw new ArgumentException("Ascension level cannot be negative", nameof(value));
            if (value > maxAscension)
                throw new ArgumentException($"Ascension level cannot exceed max {maxAscension}", nameof(value));
            if (maxAscension < 0)
                throw new ArgumentException("Max ascension cannot be negative", nameof(maxAscension));

            Value = value;
            MaxAscension = maxAscension;
        }

        public bool IsMax => Value >= MaxAscension;

        public AscensionLevel Up(int levels = 1) => new(Math.Min(Value + levels, MaxAscension), MaxAscension);

        public static implicit operator int(AscensionLevel a) => a.Value;
        public static implicit operator AscensionLevel(int i) => new(i);

        public override string ToString() => $"+{Value}/{MaxAscension}";
    }

    public readonly record struct AffinityLevel
    {
        public int Value { get; }
        public int MaxAffinity { get; }
        public long Experience { get; }
        public long ExperienceToNext { get; }

        public AffinityLevel(int value, int maxAffinity, long experience, long experienceToNext)
        {
            if (value < 0)
                throw new ArgumentException("Affinity level cannot be negative", nameof(value));
            if (value > maxAffinity)
                throw new ArgumentException($"Affinity level cannot exceed max {maxAffinity}", nameof(value));
            if (experience < 0)
                throw new ArgumentException("Experience cannot be negative", nameof(experience));

            Value = value;
            MaxAffinity = maxAffinity;
            Experience = experience;
            ExperienceToNext = experienceToNext;
        }

        public bool IsMax => Value >= MaxAffinity;
        public float Progress => ExperienceToNext > 0 ? (float)Experience / ExperienceToNext : 1f;

        public override string ToString() => $"Affinity {Value}/{MaxAffinity} ({Progress:P1})";
    }

    public readonly record struct DateTimeRange
    {
        public DateTime Start { get; }
        public DateTime End { get; }

        public DateTimeRange(DateTime start, DateTime end)
        {
            if (end < start)
                throw new ArgumentException("End date must be after start date", nameof(end));

            Start = start;
            End = end;
        }

        public bool Contains(DateTime dateTime) => dateTime >= Start && dateTime <= End;

        public bool Overlaps(DateTimeRange other) => Start <= other.End && End >= other.Start;

        public TimeSpan Duration => End - Start;

        public bool IsActive => DateTime.UtcNow >= Start && DateTime.UtcNow <= End;

        public bool IsFuture => DateTime.UtcNow < Start;

        public bool IsPast => DateTime.UtcNow > End;
    }

    public readonly record struct Probability
    {
        public float Value { get; }

        public Probability(float value)
        {
            if (value < 0f || value > 1f)
                throw new ArgumentException("Probability must be between 0 and 1", nameof(value));

            Value = value;
        }

        public static Probability FromPercentage(float percentage) => new(percentage / 100f);

        public float ToPercentage() => Value * 100f;

        public static implicit operator float(Probability p) => p.Value;
        public static implicit operator Probability(float f) => new(f);

        public override string ToString() => $"{ToPercentage():F4}%";
    }

    public readonly record struct Version
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Build { get; }

        public Version(int major, int minor, int patch, string build = "")
        {
            if (major < 0 || minor < 0 || patch < 0)
                throw new ArgumentException("Version components cannot be negative");

            Major = major;
            Minor = minor;
            Patch = patch;
            Build = build ?? "";
        }

        public static Version Parse(string versionString)
        {
            var parts = versionString.Split('.', '-');
            if (parts.Length < 3)
                throw new ArgumentException("Invalid version format");

            var major = int.Parse(parts[0]);
            var minor = int.Parse(parts[1]);
            var patchAndBuild = parts[2].Split('-');
            var patch = int.Parse(patchAndBuild[0]);
            var build = patchAndBuild.Length > 1 ? patchAndBuild[1] : "";

            return new Version(major, minor, patch, build);
        }

        public bool IsNewerThan(Version other)
        {
            if (Major != other.Major) return Major > other.Major;
            if (Minor != other.Minor) return Minor > other.Minor;
            if (Patch != other.Patch) return Patch > other.Patch;
            return string.Compare(Build, other.Build, StringComparison.Ordinal) > 0;
        }

        public override string ToString() => string.IsNullOrEmpty(Build)
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{Build}";
    }

    public readonly record struct ServerTimestamp
    {
        public DateTime Value { get; }

        public ServerTimestamp(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Server timestamp must be UTC", nameof(value));

            Value = value;
        }

        public static ServerTimestamp Now => new(DateTime.UtcNow);

        public static implicit operator DateTime(ServerTimestamp ts) => ts.Value;
        public static implicit operator ServerTimestamp(DateTime dt) => new(dt);

        public TimeSpan Age => DateTime.UtcNow - Value;

        public bool IsExpired(TimeSpan maxAge) => Age > maxAge;

        public override string ToString() => Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }
}