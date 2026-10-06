namespace Crm.Application.Customers.Attachments;

/// <summary>
/// An uploaded file as the API received it (multipart field <c>file</c> of POST /api/customers/{id}/attachments):
/// the browser's file name, its length in bytes and its content. All null / 0 when no file was sent.
/// </summary>
public sealed record UploadAttachmentRequest(string? FileName, long Length, Stream? Content);

/// <summary>A customer's file: name, server-chosen content type, size in bytes, uploader (name null when unknown), UTC time.</summary>
public sealed record CustomerAttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    Guid? UploadedById,
    string? UploadedByName,
    DateTime UploadedAt);

/// <summary>A file to send back on download. The caller disposes <see cref="Content"/> (the API does it when the response ends).</summary>
public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);
