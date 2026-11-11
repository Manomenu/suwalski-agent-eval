namespace Suwageval.Server.Tests;

public sealed class TestDatabaseTests : IDisposable
{
    private readonly DirectoryInfo checkout = Directory.CreateTempSubdirectory("suwageval-test-database-");

    private string TestDirectory => Directory.CreateDirectory(Path.Combine(checkout.FullName, "bin", "Debug")).FullName;

    [Fact]
    public void A_linked_worktree_without_its_own_connection_string_is_refused()
    {
        File.WriteAllText(Path.Combine(checkout.FullName, ".git"), "gitdir: /repos/main/.git/worktrees/feature\n");

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            TestDatabase.RefuseMainCheckoutDatabaseInLinkedWorktree(null, TestDirectory)
        );

        Assert.Contains("source scripts/.internal/checkout-env.sh", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_linked_worktree_with_its_own_connection_string_is_accepted()
    {
        File.WriteAllText(Path.Combine(checkout.FullName, ".git"), "gitdir: /repos/main/.git/worktrees/feature\n");

        TestDatabase.RefuseMainCheckoutDatabaseInLinkedWorktree("Host=localhost;Database=suwageval_test_feature", TestDirectory);
    }

    [Fact]
    public void The_main_checkout_keeps_the_plain_database()
    {
        Directory.CreateDirectory(Path.Combine(checkout.FullName, ".git"));

        TestDatabase.RefuseMainCheckoutDatabaseInLinkedWorktree(null, TestDirectory);
    }

    public void Dispose() => checkout.Delete(recursive: true);
}
