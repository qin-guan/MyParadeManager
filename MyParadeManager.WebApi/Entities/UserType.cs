namespace MyParadeManager.WebApi.Entities;

public record UserType
{
    public UserType(string value)
    {
        if (value is not "Commander" and not "Trooper")
        {
            throw new Exception("UserType must be either Commander or Trooper");
        }
        
        Value = value;
    }

    public string Value { get; init; }
}