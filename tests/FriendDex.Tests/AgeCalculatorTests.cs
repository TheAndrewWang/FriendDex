using FriendDex.Web.Services;

namespace FriendDex.Tests;

public class AgeCalculatorTests
{
    // Most tests use this as "today".
    private static readonly DateOnly Today = new(2026, 2, 25);

    // ---------- GetAge ----------

    [Fact]
    public void GetAge_BirthdayLaterThisYear_IsOneLess()
    {
        Assert.Equal(25, AgeCalculator.GetAge(new DateOnly(2000, 3, 20), Today));
    }

    [Fact]
    public void GetAge_SameMonthDayNotReached_IsOneLess()
    {
        Assert.Equal(25, AgeCalculator.GetAge(new DateOnly(2000, 2, 26), Today));
    }

    [Fact]
    public void GetAge_BirthdayToday_IsFullAge()
    {
        Assert.Equal(26, AgeCalculator.GetAge(new DateOnly(2000, 2, 25), Today));
    }

    [Fact]
    public void GetAge_BirthdayYesterday_IsFullAge()
    {
        Assert.Equal(26, AgeCalculator.GetAge(new DateOnly(2000, 2, 24), Today));
    }

    [Fact]
    public void GetAge_NoBirthday_IsNull()
    {
        Assert.Null(AgeCalculator.GetAge(null, Today));
    }

    [Fact]
    public void GetAge_Feb29OnFeb28NonLeapYear_HasNotAgedUp()
    {
        Assert.Equal(26, AgeCalculator.GetAge(new DateOnly(2000, 2, 29), new DateOnly(2027, 2, 28)));
    }

    [Fact]
    public void GetAge_Feb29OnMar1NonLeapYear_HasAgedUp()
    {
        Assert.Equal(27, AgeCalculator.GetAge(new DateOnly(2000, 2, 29), new DateOnly(2027, 3, 1)));
    }

    // ---------- NextBirthday ----------

    [Fact]
    public void NextBirthday_NoBirthday_IsNull()
    {
        Assert.Null(AgeCalculator.NextBirthday(null, Today));
    }

    [Fact]
    public void NextBirthday_BirthdayToday_IsToday()
    {
        Assert.Equal(Today, AgeCalculator.NextBirthday(new DateOnly(2000, 2, 25), Today));
    }

    [Fact]
    public void NextBirthday_BirthdayTomorrow_IsThisYear()
    {
        Assert.Equal(new DateOnly(2026, 2, 26), AgeCalculator.NextBirthday(new DateOnly(2000, 2, 26), Today));
    }

    [Fact]
    public void NextBirthday_BirthdayYesterday_IsNextYear()
    {
        Assert.Equal(new DateOnly(2027, 2, 24), AgeCalculator.NextBirthday(new DateOnly(2000, 2, 24), Today));
    }

    [Fact]
    public void NextBirthday_OnDec31ForJan1Birthday_IsNextYear()
    {
        Assert.Equal(new DateOnly(2027, 1, 1), AgeCalculator.NextBirthday(new DateOnly(2000, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void NextBirthday_Feb29InNonLeapYear_IsFeb28()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), AgeCalculator.NextBirthday(new DateOnly(2000, 2, 29), new DateOnly(2027, 2, 1)));
    }

    [Fact]
    public void NextBirthday_Feb29AfterPassingIntoLeapYear_IsFeb29()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), AgeCalculator.NextBirthday(new DateOnly(2000, 2, 29), new DateOnly(2027, 3, 1)));
    }

    [Fact]
    public void NextBirthday_Feb29InLeapYear_IsFeb29()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), AgeCalculator.NextBirthday(new DateOnly(2000, 2, 29), new DateOnly(2028, 2, 1)));
    }

    // ---------- Added edge cases ----------

    [Fact]
    public void GetAge_BornToday_IsZero()
    {
        Assert.Equal(0, AgeCalculator.GetAge(Today, Today));
    }

    [Fact]
    public void NextBirthday_BornToday_IsToday()
    {
        Assert.Equal(Today, AgeCalculator.NextBirthday(Today, Today));
    }

    [Fact]
    public void GetAge_BornEarlierThisYear_IsZero()
    {
        Assert.Equal(0, AgeCalculator.GetAge(new DateOnly(2026, 1, 1), Today));
    }

    [Fact]
    public void GetAge_Feb29OnFeb29LeapYear_HasAgedUp()
    {
        Assert.Equal(28, AgeCalculator.GetAge(new DateOnly(2000, 2, 29), new DateOnly(2028, 2, 29)));
    }

    [Fact]
    public void NextBirthday_Feb29OnFeb29LeapYear_IsToday()
    {
        var today = new DateOnly(2028, 2, 29);
        Assert.Equal(today, AgeCalculator.NextBirthday(new DateOnly(2000, 2, 29), today));
    }

    [Fact]
    public void GetAge_BornDec31OnJan1_IsOneLess()
    {
        Assert.Equal(26, AgeCalculator.GetAge(new DateOnly(2000, 12, 31), new DateOnly(2027, 1, 1)));
    }

    // Current behavior, rule not yet chosen: in a non-leap year a Feb 29 friend's
    // "next birthday" is Feb 28, but GetAge doesn't age them up until Mar 1.
    [Fact]
    public void Feb29OnFeb28NonLeapYear_BirthdayIsTodayButAgeNotYetUp()
    {
        var birthday = new DateOnly(2000, 2, 29);
        var today = new DateOnly(2027, 2, 28);

        Assert.Equal(today, AgeCalculator.NextBirthday(birthday, today));
        Assert.Equal(26, AgeCalculator.GetAge(birthday, today));
    }
}
