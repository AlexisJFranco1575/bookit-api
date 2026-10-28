using Bookit.Domain.Appointments;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;
using Xunit;

namespace Bookit.Domain.Tests.Appointments;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeRange Tomorrow = new(Now.AddDays(1), Now.AddDays(1).AddMinutes(30));

    private static Appointment Book(
        string name = "Ana Perez",
        string email = "ana@example.com",
        TimeRange? range = null) =>
        Appointment.Book(Guid.NewGuid(), Guid.NewGuid(), name, email, range ?? Tomorrow, Now);

    [Fact]
    public void Books_an_appointment_and_normalizes_the_customer_data()
    {
        var appointment = Book(name: "  Ana Perez ", email: " Ana@Example.COM ");

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Equal("Ana Perez", appointment.CustomerName);
        Assert.Equal("ana@example.com", appointment.CustomerEmail);
        Assert.Equal(Tomorrow, appointment.Range);
        Assert.Equal(Now, appointment.CreatedAt);
        Assert.NotEqual(Guid.Empty, appointment.Id);
    }

    [Fact]
    public void Rejects_an_appointment_that_starts_in_the_past()
    {
        var past = new TimeRange(Now.AddHours(-2), Now.AddHours(-1));

        Assert.Throws<DomainException>(() => { _ = Book(range: past); });
    }

    [Fact]
    public void Rejects_an_invalid_email()
    {
        Assert.Throws<DomainException>(() => { _ = Book(email: "not-an-email"); });
    }

    [Fact]
    public void Rejects_a_blank_customer_name()
    {
        Assert.Throws<DomainException>(() => { _ = Book(name: "   "); });
    }

    [Fact]
    public void Can_be_cancelled_before_it_starts()
    {
        var appointment = Book();
        var cancelledAt = Now.AddHours(1);

        appointment.Cancel(cancelledAt);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(cancelledAt, appointment.CancelledAt);
    }

    [Fact]
    public void Cannot_be_cancelled_twice()
    {
        var appointment = Book();
        appointment.Cancel(Now.AddHours(1));

        Assert.Throws<DomainException>(() => appointment.Cancel(Now.AddHours(2)));
    }

    [Fact]
    public void Cannot_be_cancelled_once_it_has_started()
    {
        var appointment = Book();

        Assert.Throws<DomainException>(() => appointment.Cancel(Tomorrow.Start.AddMinutes(1)));
    }

    [Fact]
    public void Cannot_be_completed_before_it_starts()
    {
        var appointment = Book();

        Assert.Throws<DomainException>(() => appointment.Complete(Now.AddHours(1)));
    }

    [Fact]
    public void Can_be_completed_once_it_has_started()
    {
        var appointment = Book();

        appointment.Complete(Tomorrow.Start);

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    [Fact]
    public void A_cancelled_appointment_cannot_be_completed()
    {
        var appointment = Book();
        appointment.Cancel(Now.AddHours(1));

        Assert.Throws<DomainException>(() => appointment.Complete(Tomorrow.Start));
    }

    [Fact]
    public void Can_be_marked_as_a_no_show_after_it_starts()
    {
        var appointment = Book();

        appointment.MarkNoShow(Tomorrow.End);

        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
    }
}
