using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace proyectoprogra.Models.Entities
{
    public class FidoStoredCredential
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;         // FK → AspNetUsers.Id

        [Required]
        public string Username { get; set; } = null!;       // email, for display

        [Required]
        public string UserHandleBase64 { get; set; } = null!;

        [Required]
        public string CredentialIdBase64 { get; set; } = null!;

        public byte[] PublicKey { get; set; } = null!;

        public uint SignatureCounter { get; set; }

        public string CredType { get; set; } = "public-key";

        public DateTime RegDate { get; set; } = DateTime.UtcNow;

        public Guid AaGuid { get; set; }

        // Convenience accessors – not persisted
        [NotMapped]
        public byte[] UserHandle => WebEncoders.Base64UrlDecode(UserHandleBase64);

        [NotMapped]
        public byte[] CredentialId => WebEncoders.Base64UrlDecode(CredentialIdBase64);

        [NotMapped]
        public PublicKeyCredentialDescriptor Descriptor =>
            new PublicKeyCredentialDescriptor(CredentialId);
    }
}
