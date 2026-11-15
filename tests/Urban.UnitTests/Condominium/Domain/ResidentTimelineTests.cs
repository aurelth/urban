using FluentAssertions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>
/// Ninguém entra num imóvel antes de nascer, e o cadastro de um adulto só está completo com profissão e contato de emergência.
/// As duas perguntas são do Domain, sem relógio nem banco: a data de hoje vem de fora.
/// </summary>
public class ResidentTimelineTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    [Theory]
    [InlineData("1999-03-10", true)]
    [InlineData("2000-04-30", true)]
    [InlineData("2000-05-01", false)]
    [InlineData("2000-05-02", false)]
    [InlineData("2024-03-01", false)]
    public void BirthDate_IsAfter_AnEntryDateOnlyWhenTheBirthCameLater(string entry, bool after)
    {
        BirthDate birth = BirthDate.Restore(new DateOnly(2000, 5, 1));

        birth.IsAfter(DateOnly.Parse(entry)).Should().Be(after);
    }

    [Fact]
    public void TheErrorsHaveStableCodes()
    {
        OccupancyErrors.StartBeforeBirth.Code.Should().Be("condominium.occupancy.start_before_birth");
        ResidentErrors.BirthAfterOccupancy.Code.Should().Be("condominium.resident.birth_after_occupancy");
        ResidentErrors.EmailAlreadyRegistered.Code.Should().Be("condominium.resident.email_already_registered");
        OccupancyErrors.StartBeforeBirth.Description.Should().NotBeNullOrWhiteSpace();
        ResidentErrors.BirthAfterOccupancy.Description.Should().NotBeNullOrWhiteSpace();
        ResidentErrors.EmailAlreadyRegistered.Description.Should().Be("Este e-mail já está cadastrado para outro morador");
    }

    [Fact]
    public void MissingAdultData_OfACompleteAdult_IsEmpty()
    {
        Person adult = ResidentBuilders.Resident();

        adult.MissingAdultData(Today).Should().BeEmpty();
    }

    [Fact]
    public void MissingAdultData_WithoutProfession_NamesIt()
    {
        Person adult = ResidentBuilders.Resident(withProfession: false);

        adult.MissingAdultData(Today).Should().Equal("profession");
    }

    [Fact]
    public void MissingAdultData_WithoutEmergencyContact_NamesIt()
    {
        Person adult = ResidentBuilders.Resident(withEmergencyContact: false);

        adult.MissingAdultData(Today).Should().Equal("emergencyContact");
    }

    [Fact]
    public void MissingAdultData_WithNeither_NamesBothInTheOrderOfTheScreen()
    {
        Person adult = ResidentBuilders.Resident(withProfession: false, withEmergencyContact: false);

        adult.MissingAdultData(Today).Should().Equal("profession", "emergencyContact");
    }

    [Fact]
    public void MissingAdultData_OfAMinor_IsEmpty_BecauseTheyAreExemptFromBoth()
    {
        Person minor = ResidentBuilders.Minor(birthDate: new DateOnly(2015, 4, 10));

        minor.MissingAdultData(Today).Should().BeEmpty();
    }

    [Fact]
    public void MissingAdultData_TheDayBeforeTheEighteenthBirthday_IsStillEmpty()
    {
        Person almost = ResidentBuilders.Minor(birthDate: new DateOnly(2008, 11, 14));

        almost.MissingAdultData(Today).Should().BeEmpty();
    }

    [Fact]
    public void MissingAdultData_OnTheEighteenthBirthday_AsksForBoth()
    {
        Person justAdult = ResidentBuilders.Minor(birthDate: new DateOnly(2008, 11, 13));

        justAdult.MissingAdultData(Today).Should().Equal("profession", "emergencyContact");
    }

    [Fact]
    public void MissingAdultData_AfterTheyFillInTheData_IsEmptyAgain()
    {
        Person justAdult = ResidentBuilders.Minor(birthDate: new DateOnly(2008, 11, 13));
        justAdult.UpdateResident(
            justAdult.FullName,
            justAdult.Cpf,
            justAdult.BirthDate,
            justAdult.Phone,
            justAdult.Email,
            Profession.Create("Estudante").Value,
            ResidentBuilders.SampleEmergencyContact());

        justAdult.MissingAdultData(Today).Should().BeEmpty();
    }
}
