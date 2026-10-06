using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// Cabecalhos de seguranca enviados em todas as respostas (exceto o Swagger, que tem scripts proprios).
// O principal e a CSP (Content Security Policy): o navegador so executa os scripts do proprio
// index.html (identificados pelo hash SHA-256 de cada um). Um script injetado por XSS nao roda.
public class CabecalhosDeSeguranca
{
    private readonly RequestDelegate _proximo;
    private readonly string _csp;

    public CabecalhosDeSeguranca(RequestDelegate proximo, IWebHostEnvironment ambiente)
    {
        _proximo = proximo;

        // Calcula uma vez, ao iniciar, o hash de cada <script> do index.html
        var hashes = new List<string>();
        string caminho = Path.Combine(ambiente.WebRootPath ?? "wwwroot", "index.html");
        if (File.Exists(caminho))
        {
            string html = File.ReadAllText(caminho);
            foreach (Match script in Regex.Matches(html, @"<script>([\s\S]*?)</script>"))
            {
                byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(script.Groups[1].Value));
                hashes.Add($"'sha256-{Convert.ToBase64String(hash)}'");
            }
        }

        _csp = string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' {string.Join(' ', hashes)}",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src https://fonts.gstatic.com",
            "img-src 'self' data: blob:",
            "object-src blob:",              // pre-visualizacao do PDF da nota
            "connect-src 'self'",            // o front so conversa com a propria API
            "frame-ancestors 'none'",        // ninguem coloca o sistema dentro de um iframe (clickjacking)
            "base-uri 'none'",
            "form-action 'self'");
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        var cabecalhos = contexto.Response.Headers;
        cabecalhos["X-Content-Type-Options"] = "nosniff";
        cabecalhos["X-Frame-Options"] = "DENY";
        cabecalhos["Referrer-Policy"] = "strict-origin-when-cross-origin";
        cabecalhos["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        cabecalhos["Cross-Origin-Opener-Policy"] = "same-origin";

        if (!contexto.Request.Path.StartsWithSegments("/swagger"))
        {
            cabecalhos["Content-Security-Policy"] = _csp;
        }

        await _proximo(contexto);
    }
}
