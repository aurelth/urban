using System.Net;
using FluentAssertions;
using Urban.Api.Http;

namespace Urban.IntegrationTests.Api;

public class ClientPartitionTests
{
    [Fact]
    public void A_missing_address_falls_into_one_shared_partition()
    {
        ClientPartition.KeyOf(null).Should().Be(ClientPartition.Unknown);
    }

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5")]
    public void An_IPv4_address_is_its_own_key_even_when_mapped_to_IPv6(string address, string expected)
    {
        ClientPartition.KeyOf(IPAddress.Parse(address)).Should().Be(expected);
    }

    [Fact]
    public void An_IPv6_address_is_keyed_by_its_64_bit_prefix()
    {
        ClientPartition.KeyOf(IPAddress.Parse("2001:db8:abcd:12:aaaa:bbbb:cccc:dddd")).Should().Be("2001:db8:abcd:12::/64");
    }

    [Fact]
    public void Addresses_inside_the_same_IPv6_prefix_share_a_key_and_other_prefixes_do_not()
    {
        string first = ClientPartition.KeyOf(IPAddress.Parse("2001:db8:abcd:12::1"));
        string sameNetwork = ClientPartition.KeyOf(IPAddress.Parse("2001:db8:abcd:12:ffff:ffff:ffff:ffff"));
        string otherNetwork = ClientPartition.KeyOf(IPAddress.Parse("2001:db8:abcd:13::1"));

        sameNetwork.Should().Be(first);
        otherNetwork.Should().NotBe(first);
    }
}