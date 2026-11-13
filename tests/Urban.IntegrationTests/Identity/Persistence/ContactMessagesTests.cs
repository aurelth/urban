using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Contact;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class ContactMessagesTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_stored_message_comes_back_whole_with_a_fresh_context()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        Guid condominiumId = Guid.CreateVersion7();
        ContactMessage message = ContactMessageBuilder.Unread(sender.Id, condominiumId, "Corrigir o CNPJ", "Linha um.\nLinha dois.", UserBuilder.Now);

        await AddAsync(message);

        ContactMessage stored = (await FindAsync(message.Id))!;
        stored.SenderId.Should().Be(sender.Id);
        stored.CondominiumId.Should().Be(condominiumId);
        stored.Subject.Should().Be("Corrigir o CNPJ");
        stored.Body.Should().Be("Linha um.\nLinha dois.");
        stored.CreatedAt.Should().Be(UserBuilder.Now);
        stored.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task A_message_without_a_condominium_is_stored_with_a_null_condominium()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        ContactMessage message = ContactMessageBuilder.Unread(sender.Id);

        await AddAsync(message);

        (await FindAsync(message.Id))!.CondominiumId.Should().BeNull();
    }

    [Fact]
    public async Task Finding_an_unknown_message_returns_nothing()
    {
        (await FindAsync(Guid.CreateVersion7())).Should().BeNull();
    }

    [Fact]
    public async Task A_message_from_a_sender_that_does_not_exist_is_refused_by_the_database()
    {
        ContactMessage message = ContactMessageBuilder.Unread(Guid.CreateVersion7());

        Func<Task> add = () => AddAsync(message);

        await add.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Listing_returns_the_newest_first_each_with_the_sender_account()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        ContactMessage older = ContactMessageBuilder.Unread(sender.Id, sentAt: UserBuilder.Now.AddDays(10));
        ContactMessage newer = ContactMessageBuilder.Unread(sender.Id, sentAt: UserBuilder.Now.AddDays(11));
        await AddAsync(older);
        await AddAsync(newer);

        ContactMessagePage page = await ListAsync(false, 1, 100);

        ContactMessageEntry[] mine = page.Items.Where(e => e.Message.SenderId == sender.Id).ToArray();
        mine.Select(e => e.Message.Id).Should().Equal(newer.Id, older.Id);
        mine.Should().OnlyContain(e => e.Sender.Id == sender.Id && e.Sender.Email.Value == sender.Email.Value);
    }

    [Fact]
    public async Task Listing_only_the_unread_leaves_out_the_ones_already_read()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        ContactMessage unread = ContactMessageBuilder.Unread(sender.Id);
        ContactMessage read = ContactMessageBuilder.Read(sender.Id, UserBuilder.Now.AddHours(1));
        await AddAsync(unread);
        await AddAsync(read);

        ContactMessagePage onlyUnread = await ListAsync(true, 1, 100);
        ContactMessagePage everything = await ListAsync(false, 1, 100);

        onlyUnread.Items.Select(e => e.Message.Id).Should().Contain(unread.Id).And.NotContain(read.Id);
        everything.Items.Select(e => e.Message.Id).Should().Contain([unread.Id, read.Id]);
        onlyUnread.Items.Should().OnlyContain(e => e.Message.ReadAt == null);
        everything.Total.Should().BeGreaterThanOrEqualTo(onlyUnread.Total + 1);
    }

    [Fact]
    public async Task Listing_pages_through_the_same_order_and_reports_the_total_of_every_page()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        await AddAsync(ContactMessageBuilder.Unread(sender.Id, sentAt: UserBuilder.Now.AddDays(20)));
        await AddAsync(ContactMessageBuilder.Unread(sender.Id, sentAt: UserBuilder.Now.AddDays(21)));
        await AddAsync(ContactMessageBuilder.Unread(sender.Id, sentAt: UserBuilder.Now.AddDays(22)));
        ContactMessagePage all = await ListAsync(false, 1, 100);

        ContactMessagePage first = await ListAsync(false, 1, 2);
        ContactMessagePage second = await ListAsync(false, 2, 2);

        first.Items.Should().HaveCount(2);
        first.Total.Should().Be(all.Total);
        second.Total.Should().Be(all.Total);
        first.Items.Concat(second.Items).Select(e => e.Message.Id).Should().Equal(all.Items.Take(first.Items.Count + second.Items.Count).Select(e => e.Message.Id));
    }

    [Fact]
    public async Task Marking_as_read_saves_when_it_was_read_and_changes_nothing_else()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        ContactMessage message = ContactMessageBuilder.Unread(sender.Id, Guid.CreateVersion7());
        await AddAsync(message);
        DateTimeOffset readAt = UserBuilder.Now.AddHours(2);
        message.MarkRead(readAt);

        await MarkReadAsync(message);

        ContactMessage stored = (await FindAsync(message.Id))!;
        stored.ReadAt.Should().Be(readAt);
        stored.Subject.Should().Be(message.Subject);
        stored.Body.Should().Be(message.Body);
        stored.CondominiumId.Should().Be(message.CondominiumId);
    }

    [Fact]
    public async Task Marking_as_read_twice_keeps_the_first_reading()
    {
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        ContactMessage message = ContactMessageBuilder.Unread(sender.Id);
        await AddAsync(message);
        ContactMessage first = (await FindAsync(message.Id))!;
        ContactMessage second = (await FindAsync(message.Id))!;
        DateTimeOffset firstReading = UserBuilder.Now.AddHours(1);
        first.MarkRead(firstReading);
        second.MarkRead(UserBuilder.Now.AddHours(9));

        await MarkReadAsync(first);
        await MarkReadAsync(second);

        (await FindAsync(message.Id))!.ReadAt.Should().Be(firstReading);
    }

    private async Task AddAsync(ContactMessage message)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await new ContactMessages(db).AddAsync(message, Ct);
    }

    private async Task<ContactMessage?> FindAsync(Guid id)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new ContactMessages(db).FindAsync(id, Ct);
    }

    private async Task<ContactMessagePage> ListAsync(bool unreadOnly, int page, int pageSize)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new ContactMessages(db).ListAsync(unreadOnly, page, pageSize, Ct);
    }

    private async Task MarkReadAsync(ContactMessage message)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await new ContactMessages(db).MarkReadAsync(message, Ct);
    }
}
