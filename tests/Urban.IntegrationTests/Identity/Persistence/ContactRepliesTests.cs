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
public sealed class ContactRepliesTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_stored_reply_comes_back_whole_with_the_metadata_of_its_files_and_never_their_content()
    {
        (User admin, ContactMessage message) = await SeedMessageAsync();
        byte[] pdf = SampleFiles.Pdf();
        ContactReply reply = ContactReplyBuilder.WithFiles(
            message.Id,
            admin.Id,
            [new("comprovante.pdf", pdf), new("foto.png", SampleFiles.Png())],
            "Segue o comprovante.\nObrigado!",
            UserBuilder.Now.AddHours(3));

        await AddAsync(reply);

        ContactReply stored = (await RepliesOfAsync(message.Id)).Single();
        stored.Id.Should().Be(reply.Id);
        stored.AuthorId.Should().Be(admin.Id);
        stored.Body.Should().Be("Segue o comprovante.\nObrigado!");
        stored.SentAt.Should().Be(UserBuilder.Now.AddHours(3));
        stored.Attachments.Should().Equal(
            new ContactReplyAttachment("comprovante.pdf", "application/pdf", pdf.Length),
            new ContactReplyAttachment("foto.png", "image/png", SampleFiles.Png().Length));
    }

    [Fact]
    public async Task A_reply_without_files_comes_back_with_an_empty_list()
    {
        (User admin, ContactMessage message) = await SeedMessageAsync();

        await AddAsync(ContactReplyBuilder.Text(message.Id, admin.Id));

        (await RepliesOfAsync(message.Id)).Single().Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task A_reply_to_a_message_that_does_not_exist_is_refused_by_the_database()
    {
        User admin = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.SuperAdmin);

        Func<Task> add = () => AddAsync(ContactReplyBuilder.Text(Guid.CreateVersion7(), admin.Id));

        await add.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task A_reply_from_an_author_that_does_not_exist_is_refused_by_the_database()
    {
        (_, ContactMessage message) = await SeedMessageAsync();

        Func<Task> add = () => AddAsync(ContactReplyBuilder.Text(message.Id, Guid.CreateVersion7()));

        await add.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Listing_brings_the_replies_of_each_message_oldest_first_and_only_its_own()
    {
        (User admin, ContactMessage answered) = await SeedMessageAsync();
        ContactMessage alone = ContactMessageBuilder.Unread(answered.SenderId, sentAt: UserBuilder.Now.AddMinutes(5));
        await AddMessageAsync(alone);
        ContactReply later = ContactReplyBuilder.Text(answered.Id, admin.Id, "Segunda resposta.", UserBuilder.Now.AddHours(5));
        ContactReply earlier = ContactReplyBuilder.WithFiles(answered.Id, admin.Id, [new("ata.pdf", SampleFiles.Pdf())], "Primeira resposta.", UserBuilder.Now.AddHours(4));
        await AddAsync(later);
        await AddAsync(earlier);

        ContactMessagePage page = await ListAsync();

        ContactMessageEntry withReplies = page.Items.Single(e => e.Message.Id == answered.Id);
        withReplies.Replies!.Select(r => r.Id).Should().Equal(earlier.Id, later.Id);
        withReplies.Replies![0].Attachments.Should().ContainSingle().Which.FileName.Should().Be("ata.pdf");
        page.Items.Single(e => e.Message.Id == alone.Id).Replies.Should().BeNullOrEmpty();
    }

    private async Task<(User Admin, ContactMessage Message)> SeedMessageAsync()
    {
        User admin = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.SuperAdmin);
        User sender = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        ContactMessage message = ContactMessageBuilder.Unread(sender.Id);
        await AddMessageAsync(message);

        return (admin, message);
    }

    private async Task AddMessageAsync(ContactMessage message)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await new ContactMessages(db).AddAsync(message, Ct);
    }

    private async Task AddAsync(ContactReply reply)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await new ContactReplies(db).AddAsync(reply, Ct);
    }

    private async Task<ContactReply[]> RepliesOfAsync(Guid messageId)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await db.ContactReplies.AsNoTracking().Where(r => r.MessageId == messageId).OrderBy(r => r.SentAt).ToArrayAsync(Ct);
    }

    private async Task<ContactMessagePage> ListAsync()
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new ContactMessages(db).ListAsync(false, 1, 100, Ct);
    }
}
