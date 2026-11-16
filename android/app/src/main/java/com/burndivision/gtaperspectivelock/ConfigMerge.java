package com.burndivision.gtaperspectivelock;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.function.Consumer;

/** Android-independent, top-level JSON overlay. Objects and arrays replace whole values. */
final class ConfigMerge {
    private ConfigMerge() {}

    static String merge(String defaults, String user) throws IOException {
        Map<String, String> values = new Parser(defaults).object();
        if (user != null) values.putAll(new Parser(user).object());
        StringBuilder out = new StringBuilder("{\n");
        for (Map.Entry<String, String> e : values.entrySet()) {
            if (out.length() > 2) out.append(",\n");
            out.append("  ").append(e.getKey()).append(": ").append(e.getValue());
        }
        return out.append("\n}\n").toString();
    }

    static void install(Path target, String defaults, Consumer<String> log) throws IOException {
        // Validate defaults before touching either the user file or its backup.
        String result = merge(defaults, null);
        byte[] previous = Files.exists(target) ? Files.readAllBytes(target) : null;
        if (previous == null) {
            log.accept("sys_config.json missing; using APK defaults");
        } else {
            try {
                result = merge(defaults, new String(previous, StandardCharsets.UTF_8));
            } catch (IOException e) {
                log.accept("sys_config.json unparsable; preserving as .bak and using APK defaults: " + e.getMessage());
            }
            atomicWrite(target.resolveSibling(target.getFileName() + ".bak"), previous);
        }
        atomicWrite(target, result.getBytes(StandardCharsets.UTF_8));
    }

    private static void atomicWrite(Path target, byte[] bytes) throws IOException {
        Files.createDirectories(target.toAbsolutePath().getParent());
        Path temp = Files.createTempFile(target.toAbsolutePath().getParent(), ".config-", ".tmp");
        try {
            Files.write(temp, bytes);
            // Fail rather than silently fall back to a non-atomic replacement.
            Files.move(temp, target.toAbsolutePath(), StandardCopyOption.ATOMIC_MOVE,
                    StandardCopyOption.REPLACE_EXISTING);
        } finally {
            Files.deleteIfExists(temp);
        }
    }

    /** Small JSON reader retaining value text, with // and block comments outside strings. */
    private static final class Parser {
        private final String text;
        private int pos;
        Parser(String text) throws IOException {
            StringBuilder clean = new StringBuilder();
            boolean quoted = false, escaped = false;
            for (int i = 0; i < text.length(); i++) {
                char c = text.charAt(i);
                if (!quoted && c == '/' && i + 1 < text.length()) {
                    char next = text.charAt(i + 1);
                    if (next == '/') {
                        while (i + 1 < text.length() && text.charAt(i + 1) != '\n') i++;
                        clean.append(' ');
                        continue;
                    }
                    if (next == '*') {
                        int end = text.indexOf("*/", i + 2);
                        if (end < 0) throw new IOException("Unterminated comment");
                        i = end + 1;
                        clean.append(' ');
                        continue;
                    }
                }
                clean.append(c);
                if (quoted && escaped) escaped = false;
                else if (quoted && c == '\\') escaped = true;
                else if (c == '"') quoted = !quoted;
            }
            this.text = clean.toString();
        }
        Map<String, String> object() throws IOException {
            Map<String, String> out = readObject(0);
            space();
            if (pos != text.length()) fail();
            return out;
        }
        private Map<String, String> readObject(int depth) throws IOException {
            expect('{');
            Map<String, String> out = new LinkedHashMap<>();
            if (take('}')) return out;
            do {
                space();
                String key = string();
                expect(':');
                space();
                int valueStart = pos;
                value(depth + 1);
                // Canonical key spelling makes escaped and literal equivalent names collide.
                out.put(quote(key), text.substring(valueStart, pos));
            } while (take(','));
            expect('}');
            return out;
        }
        private void value(int depth) throws IOException {
            if (depth > 128) throw new IOException("JSON nesting exceeds 128");
            space();
            if (pos >= text.length()) { fail(); return; }
            char c = text.charAt(pos);
            if (c == '{') readObject(depth);
            else if (c == '[') {
                pos++;
                if (!take(']')) {
                    do { value(depth + 1); } while (take(','));
                    expect(']');
                }
            } else if (c == '"') string();
            else {
                int start = pos;
                while (pos < text.length() && ",]} \t\r\n".indexOf(text.charAt(pos)) < 0) pos++;
                String token = text.substring(start, pos);
                if (!token.matches("true|false|null|-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][+-]?[0-9]+)?")) fail();
            }
        }
        private String string() throws IOException {
            expect('"');
            StringBuilder out = new StringBuilder();
            while (pos < text.length()) {
                char c = text.charAt(pos++);
                if (c == '"') return out.toString();
                if (c < 32) fail();
                if (c == '\\') {
                    if (pos >= text.length()) fail();
                    c = text.charAt(pos++);
                    int escape = "\"\\/bfnrt".indexOf(c);
                    if (escape >= 0) c = "\"\\/\b\f\n\r\t".charAt(escape);
                    else if (c == 'u') {
                        if (pos + 4 > text.length()) fail();
                        if (!text.substring(pos, pos + 4).matches("[0-9a-fA-F]{4}")) fail();
                        try { c = (char) Integer.parseInt(text.substring(pos, pos + 4), 16); }
                        catch (NumberFormatException e) { throw new IOException("Invalid unicode escape", e); }
                        pos += 4;
                    } else fail();
                }
                out.append(c);
            }
            fail();
            return "";
        }
        private static String quote(String s) {
            StringBuilder out = new StringBuilder("\"");
            for (char c : s.toCharArray()) {
                if (c == '"' || c == '\\') out.append('\\').append(c);
                else if (c < 32) out.append(String.format("\\u%04x", (int) c));
                else out.append(c);
            }
            return out.append('"').toString();
        }
        private void space() {
            while (pos < text.length() && " \t\r\n".indexOf(text.charAt(pos)) >= 0) pos++;
        }
        private boolean take(char c) {
            space();
            if (pos < text.length() && text.charAt(pos) == c) { pos++; return true; }
            return false;
        }
        private void expect(char c) throws IOException { if (!take(c)) fail(); }
        private void fail() throws IOException { throw new IOException("Invalid JSON at offset " + pos); }
    }
}
