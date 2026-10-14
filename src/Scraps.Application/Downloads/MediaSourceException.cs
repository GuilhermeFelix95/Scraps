namespace Scraps.Application.Downloads;

// Erro de entrada do usuário: a mensagem já está pronta para ser exibida.
public sealed class MediaSourceException(string message) : Exception(message);
