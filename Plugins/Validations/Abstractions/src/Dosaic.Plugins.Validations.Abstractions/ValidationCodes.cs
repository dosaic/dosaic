namespace Dosaic.Plugins.Validations.Abstractions;

public class ValidationCodes
{
    public const string GenericError = "InternalError";
    public const string Required = "Required";
    public const string Expression = "Expression";
    public const string Unique = "Unique";
    public const string InvalidType = "InvalidType";

    public class String
    {
        public const string MinLength = "String.MinLength";
        public const string MaxLength = "String.MaxLength";
        public const string Length = "String.Length";
        public const string Regex = "String.Regex";
        public const string Email = "String.Email";
        public const string Url = "String.Url";
    }

    public class Int
    {
        public const string Range = "Number.Range";
        public const string Max = "Number.Max";
        public const string Min = "Number.Min";
        public const string Negative = "Number.Negative";
        public const string Positive = "Number.Positive";
    }

    public class Date
    {
        public const string Before = "Date.Before";
        public const string After = "Date.After";
        public const string Age = "Date.Age";
        public const string MinAge = "Date.MinAge";
        public const string MaxAge = "Date.MaxAge";
    }

    public class Bool
    {
        public const string True = "Bool.True";
        public const string False = "Bool.False";
    }

    public class Array
    {
        public const string Length = "Array.Length";
        public const string MinLength = "Array.MinLength";
        public const string MaxLength = "Array.MaxLength";
    }
}
