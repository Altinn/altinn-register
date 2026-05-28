namespace Altinn.Register.TestUtils.Sftp;

public record class SftpServerInfo(
    string Host,
    int Port,
    string Username,
    string Password,
    string UploadDirectory);
