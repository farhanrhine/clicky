/**
 * Clicky Proxy Worker
 *
 * Proxies requests to the AI APIs so the app never ships with raw API keys.
 * Keys are stored as Cloudflare Worker secrets.
 *
 * Routes:
 *   POST /chat             → Claude (prod) or Groq Llama 4 Scout (dev)
 *   POST /tts              → ElevenLabs (prod) or Groq Orpheus TTS (dev)
 *   POST /transcribe-token → AssemblyAI temp token (prod) or Groq key stub (dev)
 *
 * DEV MODE:
 *   Set the DEV_MODE secret to "true" to route all traffic through Groq.
 *   This lets you develop for free using Groq's free tier — no Claude,
 *   ElevenLabs, or AssemblyAI credits consumed.
 *   Set DEV_MODE to "false" (or leave unset) to use production APIs.
 *
 *   Switch:  npx wrangler secret put DEV_MODE  → type "true" or "false"
 *
 * Groq dev models:
 *   Chat:   meta-llama/llama-4-scout-17b-16e-instruct  (vision-capable)
 *   TTS:    canopylabs/orpheus-v1-english
 *   STT:    whisper-large-v3-turbo (upload-based, returned as a usable key)
 */

interface Env {
  // Production API keys
  ANTHROPIC_API_KEY: string;
  ELEVENLABS_API_KEY: string;
  ELEVENLABS_VOICE_ID: string;
  ASSEMBLYAI_API_KEY: string;

  // Groq API key — used when DEV_MODE is "true"
  GROQ_API_KEY: string;

  // Set to "true" to route all traffic through Groq (free dev mode)
  // Set to "false" or leave unset to use production APIs
  DEV_MODE: string;
}

// Groq model IDs used in dev mode
const GROQ_CHAT_MODEL = "meta-llama/llama-4-scout-17b-16e-instruct";
const GROQ_TTS_MODEL  = "canopylabs/orpheus-v1-english";
const GROQ_STT_MODEL  = "whisper-large-v3-turbo";

// Groq base URL (OpenAI-compatible)
const GROQ_BASE_URL = "https://api.groq.com/openai/v1";

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (request.method !== "POST") {
      return new Response("Method not allowed", { status: 405 });
    }

    // Determine whether to use Groq or production APIs.
    // DEV_MODE must be explicitly set to the string "true" to activate.
    const isDevMode = env.DEV_MODE === "true";

    // Log the active mode on every request so it is visible in wrangler tail
    console.log(`[mode] ${isDevMode ? "DEV (Groq)" : "PROD (Claude/ElevenLabs/AssemblyAI)"} — ${url.pathname}`);

    try {
      if (url.pathname === "/chat") {
        return isDevMode
          ? await handleChatGroq(request, env)
          : await handleChatClaude(request, env);
      }

      if (url.pathname === "/tts") {
        return isDevMode
          ? await handleTtsGroq(request, env)
          : await handleTtsElevenLabs(request, env);
      }

      if (url.pathname === "/transcribe-token") {
        return isDevMode
          ? await handleTranscribeTokenGroq(env)
          : await handleTranscribeTokenAssemblyAI(env);
      }
    } catch (error) {
      console.error(`[${url.pathname}] Unhandled error:`, error);
      return new Response(
        JSON.stringify({ error: String(error) }),
        { status: 500, headers: { "content-type": "application/json" } }
      );
    }

    return new Response("Not found", { status: 404 });
  },
};

// ── /chat ────────────────────────────────────────────────────────────────────

/**
 * PRODUCTION: proxies to Anthropic's Claude Messages API with SSE streaming.
 * The app sends the full Anthropic-format request body unchanged.
 */
async function handleChatClaude(request: Request, env: Env): Promise<Response> {
  const body = await request.text();

  const response = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: {
      "x-api-key": env.ANTHROPIC_API_KEY,
      "anthropic-version": "2023-06-01",
      "content-type": "application/json",
    },
    body,
  });

  if (!response.ok) {
    const errorBody = await response.text();
    console.error(`[/chat][claude] error ${response.status}: ${errorBody}`);
    return new Response(errorBody, {
      status: response.status,
      headers: { "content-type": "application/json" },
    });
  }

  return new Response(response.body, {
    status: response.status,
    headers: {
      "content-type": response.headers.get("content-type") || "text/event-stream",
      "cache-control": "no-cache",
    },
  });
}

/**
 * DEV: proxies to Groq's OpenAI-compatible chat endpoint using Llama 4 Scout.
 * Llama 4 Scout supports vision (images in the message content array),
 * so screenshot attachments work exactly as they do with Claude.
 *
 * The app sends an Anthropic-format body. We translate it to OpenAI format
 * here because Groq uses the OpenAI schema, not the Anthropic schema.
 */
async function handleChatGroq(request: Request, env: Env): Promise<Response> {
  const anthropicBody = await request.json() as any;

  // Translate Anthropic messages format → OpenAI messages format.
  // Both use a "messages" array; the main difference is:
  //   - Anthropic wraps content in [{type:"text",text:...}, {type:"image",...}]
  //   - OpenAI uses the same structure for vision — so we can pass it through
  //     directly, just swapping the top-level keys.
  const openAIBody = {
    model: GROQ_CHAT_MODEL,
    max_tokens: anthropicBody.max_tokens ?? 1024,
    stream: anthropicBody.stream ?? true,
    messages: [
      // Inject the system prompt as a system role message (OpenAI style)
      ...(anthropicBody.system
        ? [{ role: "system", content: anthropicBody.system }]
        : []),
      // Pass through the conversation messages unchanged — OpenAI and
      // Anthropic use the same content-block format for vision
      ...(anthropicBody.messages ?? []),
    ],
  };

  const response = await fetch(`${GROQ_BASE_URL}/chat/completions`, {
    method: "POST",
    headers: {
      "Authorization": `Bearer ${env.GROQ_API_KEY}`,
      "content-type": "application/json",
    },
    body: JSON.stringify(openAIBody),
  });

  if (!response.ok) {
    const errorBody = await response.text();
    console.error(`[/chat][groq] error ${response.status}: ${errorBody}`);
    return new Response(errorBody, {
      status: response.status,
      headers: { "content-type": "application/json" },
    });
  }

  return new Response(response.body, {
    status: response.status,
    headers: {
      "content-type": response.headers.get("content-type") || "text/event-stream",
      "cache-control": "no-cache",
    },
  });
}

// ── /tts ─────────────────────────────────────────────────────────────────────

/**
 * PRODUCTION: proxies to ElevenLabs TTS and returns MP3 audio bytes.
 */
async function handleTtsElevenLabs(request: Request, env: Env): Promise<Response> {
  const body = await request.text();
  const voiceId = env.ELEVENLABS_VOICE_ID;

  const response = await fetch(
    `https://api.elevenlabs.io/v1/text-to-speech/${voiceId}`,
    {
      method: "POST",
      headers: {
        "xi-api-key": env.ELEVENLABS_API_KEY,
        "content-type": "application/json",
        accept: "audio/mpeg",
      },
      body,
    }
  );

  if (!response.ok) {
    const errorBody = await response.text();
    console.error(`[/tts][elevenlabs] error ${response.status}: ${errorBody}`);
    return new Response(errorBody, {
      status: response.status,
      headers: { "content-type": "application/json" },
    });
  }

  return new Response(response.body, {
    status: response.status,
    headers: {
      "content-type": response.headers.get("content-type") || "audio/mpeg",
    },
  });
}

/**
 * DEV: proxies to Groq's OpenAI-compatible TTS endpoint using Orpheus V1.
 * Orpheus supports expressive vocal controls like [cheerful] and [whisper].
 * The app sends { text, ... } — we extract the text and call Groq TTS.
 */
async function handleTtsGroq(request: Request, env: Env): Promise<Response> {
  const incomingBody = await request.json() as any;
  const textToSpeak: string = incomingBody.text ?? "";

  const groqTtsBody = {
    model: GROQ_TTS_MODEL,
    input: textToSpeak,
    voice: "Fritz-PlayAI", // Orpheus V1 English default voice
    response_format: "mp3",
  };

  const response = await fetch(`${GROQ_BASE_URL}/audio/speech`, {
    method: "POST",
    headers: {
      "Authorization": `Bearer ${env.GROQ_API_KEY}`,
      "content-type": "application/json",
    },
    body: JSON.stringify(groqTtsBody),
  });

  if (!response.ok) {
    const errorBody = await response.text();
    console.error(`[/tts][groq] error ${response.status}: ${errorBody}`);
    return new Response(errorBody, {
      status: response.status,
      headers: { "content-type": "application/json" },
    });
  }

  return new Response(response.body, {
    status: response.status,
    headers: {
      "content-type": response.headers.get("content-type") || "audio/mpeg",
    },
  });
}

// ── /transcribe-token ────────────────────────────────────────────────────────

/**
 * PRODUCTION: fetches a short-lived (480s) AssemblyAI WebSocket token.
 * The app opens a WebSocket with this token for real-time streaming STT.
 */
async function handleTranscribeTokenAssemblyAI(env: Env): Promise<Response> {
  const response = await fetch(
    "https://streaming.assemblyai.com/v3/token?expires_in_seconds=480",
    {
      method: "GET",
      headers: {
        authorization: env.ASSEMBLYAI_API_KEY,
      },
    }
  );

  if (!response.ok) {
    const errorBody = await response.text();
    console.error(`[/transcribe-token][assemblyai] error ${response.status}: ${errorBody}`);
    return new Response(errorBody, {
      status: response.status,
      headers: { "content-type": "application/json" },
    });
  }

  const data = await response.text();
  return new Response(data, {
    status: 200,
    headers: { "content-type": "application/json" },
  });
}

/**
 * DEV: returns the Groq API key in the same { token } shape that the app
 * expects from the AssemblyAI token endpoint.
 *
 * In dev mode the app uses Groq Whisper (upload-based) instead of AssemblyAI
 * (streaming WebSocket). The app buffers audio while Ctrl+Alt is held, then
 * POSTs the WAV to Groq's /audio/transcriptions endpoint on key release.
 * The "token" here is just the Groq key the app will use for that upload.
 */
async function handleTranscribeTokenGroq(env: Env): Promise<Response> {
  // Return in the same shape as AssemblyAI's token response so the app
  // does not need any conditional logic — it reads { token } either way.
  return new Response(
    JSON.stringify({ token: env.GROQ_API_KEY, provider: "groq" }),
    {
      status: 200,
      headers: { "content-type": "application/json" },
    }
  );
}
