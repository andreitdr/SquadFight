namespace Game.Core
{
    public enum ErrorCode
    {
        None = 0,
        Unknown,
        InvalidArgument,
        NotFound,
        InsufficientFunds,
        InsufficientShards,
        AlreadyUnlocked,
        NotUnlocked,
        MaxStarReached,
        PartyFull,
        DuplicateInParty,
        HeroLocked,
        InvalidSlot,
        SlotIncompatible,
        ItemEquipped,
        InventoryFull,
        FightLocked,
        AlreadyClaimed,
        FeatureDisabled,
        MaxSkillLevelReached,
    }

    /// <summary>Outcome of a rule-checked mutation. Rule violations never throw.</summary>
    public readonly struct Result
    {
        public readonly ErrorCode Code;
        public readonly string Detail;

        private Result(ErrorCode code, string detail)
        {
            Code = code;
            Detail = detail;
        }

        public bool Ok => Code == ErrorCode.None;
        public static Result Success => default;
        public static Result Fail(ErrorCode code, string detail = null) => new Result(code, detail);
        public override string ToString() => Ok ? "Ok" : string.IsNullOrEmpty(Detail) ? Code.ToString() : $"{Code}: {Detail}";
    }

    public readonly struct Result<T>
    {
        public readonly ErrorCode Code;
        public readonly string Detail;
        public readonly T Value;

        private Result(ErrorCode code, string detail, T value)
        {
            Code = code;
            Detail = detail;
            Value = value;
        }

        public bool Ok => Code == ErrorCode.None;
        public static Result<T> Success(T value) => new Result<T>(ErrorCode.None, null, value);
        public static Result<T> Fail(ErrorCode code, string detail = null) => new Result<T>(code, detail, default);
        public Result AsResult() => Ok ? Result.Success : Result.Fail(Code, Detail);
        public override string ToString() => Ok ? $"Ok({Value})" : $"{Code}: {Detail}";
    }
}
