namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdempotencyFingerprinter
{
    string HashKey(string key);

    string Fingerprint(string value);

    string Fingerprint(string name, string email, string password);
}
