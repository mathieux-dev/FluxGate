'use client';

import { useState } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Copy, Check } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

interface CodeExamplesProps {
  endpoint: {
    method: string;
    path: string;
    requestBody?: Record<string, any>;
  };
  apiKey: string;
}

export function APICodeExamples({ endpoint, apiKey }: CodeExamplesProps) {
  const { toast } = useToast();
  const [copiedLang, setCopiedLang] = useState<string | null>(null);

  const copyToClipboard = (text: string, lang: string) => {
    navigator.clipboard.writeText(text);
    setCopiedLang(lang);
    toast({
      title: 'Copiado!',
      description: 'Código copiado para a área de transferência',
    });
    setTimeout(() => setCopiedLang(null), 2000);
  };

  const generateJavaScriptExample = () => {
    const hasBody = endpoint.requestBody && endpoint.method !== 'GET';
    const bodyStr = hasBody ? JSON.stringify(endpoint.requestBody, null, 2) : '';
    
    return `const crypto = require('crypto');

// HMAC Signature Generation
function generateHMACSignature(apiKey, apiSecret, method, path, body) {
  const timestamp = Date.now().toString();
  const nonce = crypto.randomBytes(16).toString('hex');
  
  const bodyHash = body 
    ? crypto.createHash('sha256').update(JSON.stringify(body)).digest('hex')
    : '';
  
  const message = \`\${timestamp}.\${nonce}.\${method}.\${path}.\${bodyHash}\`;
  const signature = crypto
    .createHmac('sha256', apiSecret)
    .update(message)
    .digest('base64');
  
  return { timestamp, nonce, signature };
}

// Make API Request
const apiKey = '${apiKey}';
const apiSecret = 'your_api_secret'; // Never expose this!

const method = '${endpoint.method}';
const path = '${endpoint.path}';
${hasBody ? `const body = ${bodyStr};` : ''}

const { timestamp, nonce, signature } = generateHMACSignature(
  apiKey,
  apiSecret,
  method,
  path,
  ${hasBody ? 'body' : 'null'}
);

fetch(\`https://api.fluxpay.com\${path}\`, {
  method: method,
  headers: {
    'Content-Type': 'application/json',
    'X-Api-Key': apiKey,
    'X-Timestamp': timestamp,
    'X-Nonce': nonce,
    'X-Signature': signature
  },
  ${hasBody ? 'body: JSON.stringify(body)' : ''}
})
  .then(res => res.json())
  .then(data => console.log(data))
  .catch(err => console.error(err));`;
  };

  const generatePythonExample = () => {
    const hasBody = endpoint.requestBody && endpoint.method !== 'GET';
    const bodyStr = hasBody ? JSON.stringify(endpoint.requestBody, null, 2) : '';
    
    return `import hmac
import hashlib
import time
import secrets
import json
import requests

# HMAC Signature Generation
def generate_hmac_signature(api_key, api_secret, method, path, body=None):
    timestamp = str(int(time.time() * 1000))
    nonce = secrets.token_hex(16)
    
    body_hash = ''
    if body:
        body_hash = hashlib.sha256(json.dumps(body).encode()).hexdigest()
    
    message = f"{timestamp}.{nonce}.{method}.{path}.{body_hash}"
    signature = hmac.new(
        api_secret.encode(),
        message.encode(),
        hashlib.sha256
    ).digest()
    signature_b64 = base64.b64encode(signature).decode()
    
    return timestamp, nonce, signature_b64

# Make API Request
api_key = '${apiKey}'
api_secret = 'your_api_secret'  # Never expose this!

method = '${endpoint.method}'
path = '${endpoint.path}'
${hasBody ? `body = ${bodyStr}` : ''}

timestamp, nonce, signature = generate_hmac_signature(
    api_key,
    api_secret,
    method,
    path,
    ${hasBody ? 'body' : 'None'}
)

headers = {
    'Content-Type': 'application/json',
    'X-Api-Key': api_key,
    'X-Timestamp': timestamp,
    'X-Nonce': nonce,
    'X-Signature': signature
}

response = requests.${endpoint.method.toLowerCase()}(
    f'https://api.fluxpay.com{path}',
    headers=headers,
    ${hasBody ? 'json=body' : ''}
)

print(response.json())`;
  };

  const generatePHPExample = () => {
    const hasBody = endpoint.requestBody && endpoint.method !== 'GET';
    const bodyStr = hasBody ? JSON.stringify(endpoint.requestBody, null, 2) : '';
    
    return `<?php

// HMAC Signature Generation
function generateHMACSignature($apiKey, $apiSecret, $method, $path, $body = null) {
    $timestamp = (string)(time() * 1000);
    $nonce = bin2hex(random_bytes(16));
    
    $bodyHash = '';
    if ($body) {
        $bodyHash = hash('sha256', json_encode($body));
    }
    
    $message = "$timestamp.$nonce.$method.$path.$bodyHash";
    $signature = base64_encode(
        hash_hmac('sha256', $message, $apiSecret, true)
    );
    
    return [
        'timestamp' => $timestamp,
        'nonce' => $nonce,
        'signature' => $signature
    ];
}

// Make API Request
$apiKey = '${apiKey}';
$apiSecret = 'your_api_secret'; // Never expose this!

$method = '${endpoint.method}';
$path = '${endpoint.path}';
${hasBody ? `$body = json_decode('${bodyStr}', true);` : '$body = null;'}

$hmac = generateHMACSignature($apiKey, $apiSecret, $method, $path, $body);

$ch = curl_init("https://api.fluxpay.com$path");
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_CUSTOMREQUEST, $method);
curl_setopt($ch, CURLOPT_HTTPHEADER, [
    'Content-Type: application/json',
    "X-Api-Key: $apiKey",
    "X-Timestamp: {$hmac['timestamp']}",
    "X-Nonce: {$hmac['nonce']}",
    "X-Signature: {$hmac['signature']}"
]);
${hasBody ? 'curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body));' : ''}

$response = curl_exec($ch);
curl_close($ch);

echo $response;`;
  };

  const generateCSharpExample = () => {
    const hasBody = endpoint.requestBody && endpoint.method !== 'GET';
    const bodyStr = hasBody ? JSON.stringify(endpoint.requestBody, null, 2) : '';
    
    return `using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class FluxPayClient
{
    // HMAC Signature Generation
    public static (string timestamp, string nonce, string signature) GenerateHMACSignature(
        string apiKey, 
        string apiSecret, 
        string method, 
        string path, 
        object body = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        
        var bodyHash = "";
        if (body != null)
        {
            var bodyJson = JsonSerializer.Serialize(body);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(bodyJson));
            bodyHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
        
        var message = $"{timestamp}.{nonce}.{method}.{path}.{bodyHash}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToBase64String(signatureBytes);
        
        return (timestamp, nonce, signature);
    }
    
    // Make API Request
    public static async Task Main()
    {
        var apiKey = "${apiKey}";
        var apiSecret = "your_api_secret"; // Never expose this!
        
        var method = "${endpoint.method}";
        var path = "${endpoint.path}";
        ${hasBody ? `var body = JsonSerializer.Deserialize<object>("${bodyStr.replace(/"/g, '\\"')}");` : 'object body = null;'}
        
        var (timestamp, nonce, signature) = GenerateHMACSignature(
            apiKey, 
            apiSecret, 
            method, 
            path, 
            body
        );
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Add("X-Timestamp", timestamp);
        client.DefaultRequestHeaders.Add("X-Nonce", nonce);
        client.DefaultRequestHeaders.Add("X-Signature", signature);
        
        HttpResponseMessage response;
        ${hasBody ? `
        var content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json"
        );
        response = await client.${endpoint.method === 'POST' ? 'PostAsync' : endpoint.method === 'PUT' ? 'PutAsync' : 'DeleteAsync'}(
            $"https://api.fluxpay.com{path}",
            content
        );` : `
        response = await client.GetAsync($"https://api.fluxpay.com{path}");`}
        
        var result = await response.Content.ReadAsStringAsync();
        Console.WriteLine(result);
    }
}`;
  };

  return (
    <Tabs defaultValue="javascript" className="w-full">
      <TabsList className="grid w-full grid-cols-4">
        <TabsTrigger value="javascript">JavaScript</TabsTrigger>
        <TabsTrigger value="python">Python</TabsTrigger>
        <TabsTrigger value="php">PHP</TabsTrigger>
        <TabsTrigger value="csharp">C#</TabsTrigger>
      </TabsList>

      <TabsContent value="javascript" className="relative">
        <pre className="bg-muted p-4 rounded text-xs overflow-auto max-h-[500px]">
          {generateJavaScriptExample()}
        </pre>
        <Button
          size="sm"
          variant="ghost"
          className="absolute top-2 right-2"
          onClick={() => copyToClipboard(generateJavaScriptExample(), 'javascript')}
        >
          {copiedLang === 'javascript' ? (
            <Check className="h-4 w-4" />
          ) : (
            <Copy className="h-4 w-4" />
          )}
        </Button>
      </TabsContent>

      <TabsContent value="python" className="relative">
        <pre className="bg-muted p-4 rounded text-xs overflow-auto max-h-[500px]">
          {generatePythonExample()}
        </pre>
        <Button
          size="sm"
          variant="ghost"
          className="absolute top-2 right-2"
          onClick={() => copyToClipboard(generatePythonExample(), 'python')}
        >
          {copiedLang === 'python' ? (
            <Check className="h-4 w-4" />
          ) : (
            <Copy className="h-4 w-4" />
          )}
        </Button>
      </TabsContent>

      <TabsContent value="php" className="relative">
        <pre className="bg-muted p-4 rounded text-xs overflow-auto max-h-[500px]">
          {generatePHPExample()}
        </pre>
        <Button
          size="sm"
          variant="ghost"
          className="absolute top-2 right-2"
          onClick={() => copyToClipboard(generatePHPExample(), 'php')}
        >
          {copiedLang === 'php' ? (
            <Check className="h-4 w-4" />
          ) : (
            <Copy className="h-4 w-4" />
          )}
        </Button>
      </TabsContent>

      <TabsContent value="csharp" className="relative">
        <pre className="bg-muted p-4 rounded text-xs overflow-auto max-h-[500px]">
          {generateCSharpExample()}
        </pre>
        <Button
          size="sm"
          variant="ghost"
          className="absolute top-2 right-2"
          onClick={() => copyToClipboard(generateCSharpExample(), 'csharp')}
        >
          {copiedLang === 'csharp' ? (
            <Check className="h-4 w-4" />
          ) : (
            <Copy className="h-4 w-4" />
          )}
        </Button>
      </TabsContent>
    </Tabs>
  );
}
