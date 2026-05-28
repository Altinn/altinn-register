using Microsoft.Extensions.Configuration;

namespace Altinn.Register.TestUtils.Sftp;

public record class SftpServerInfo(
    string Host,
    int Port,
    string Username,
    string Password,
    string UploadDirectory)
{
    /// <summary>
    /// Writes this server's connection details into <paramref name="configuration"/> under
    /// <paramref name="sectionKey"/>, using property names that match the shape of
    /// <c>SftpClientSettings</c> (<c>Host</c>, <c>Port</c>, <c>User</c>, <c>Password</c>,
    /// <c>RemotePath</c>).
    /// </summary>
    /// <param name="configuration">The <see cref="IConfigurationBuilder"/> to write into.</param>
    /// <param name="sectionKey">The configuration section to write under (no trailing colon).</param>
    public void Configure(IConfigurationBuilder configuration, string sectionKey)
    {
        configuration.AddInMemoryCollection([
            new($"{sectionKey}:Host", Host),
            new($"{sectionKey}:Port", Port.ToString()),
            new($"{sectionKey}:User", Username),
            new($"{sectionKey}:Password", Password),
            new($"{sectionKey}:RemotePath", UploadDirectory),
        ]);
    }
}
