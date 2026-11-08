namespace FriendDex.Web.Services;

public static class AgeCalculator
{
    public static int? GetAge(DateOnly? birthday, DateOnly today)
    {
        if (birthday == null) return null;
        var b = birthday.Value;
        int age = today.Year - b.Year;
        if (today.Month < b.Month || (today.Month == b.Month && today.Day < b.Day))
        {
            age--;
        }
        return age;
    }

    public static DateOnly? NextBirthday(DateOnly? birthday, DateOnly today)
    {
        if (birthday == null) return null;
        var b = birthday.Value;
        
        var age = GetAge(birthday, today) ?? 0;
        var next = b.AddYears(age);
        if (next < today)
        {
            next = b.AddYears(age + 1);
        }

        return next;
    }
}