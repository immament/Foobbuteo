// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

package com.imma.foobbuteo;

import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.AtomicFile;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.util.Arrays;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

public final class RememberedSessionStorage {
    private static final String ALIAS = "foobbuteo.remembered-session.v1";

    private static AtomicFile file(Context context) {
        return new AtomicFile(new File(context.getNoBackupFilesDir(), "foobbuteo-session.v1"));
    }

    private static KeyStore keyStore() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        return store;
    }

    private static SecretKey key(boolean create) throws Exception {
        KeyStore store = keyStore();
        if (store.containsAlias(ALIAS)) return (SecretKey) store.getKey(ALIAS, null);
        if (!create) throw new IllegalStateException("Remembered session key is unavailable");
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setRandomizedEncryptionRequired(true)
            .setKeySize(256).build());
        return generator.generateKey();
    }

    public static synchronized String load(Context context) throws Exception {
        AtomicFile storage = file(context);
        if (!storage.getBaseFile().exists()) return null;
        byte[] bytes = storage.readFully();
        if (bytes.length < 29 || bytes.length > 4096 || bytes[0] != 1)
            throw new IllegalStateException("Invalid remembered session");
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.DECRYPT_MODE, key(false), new GCMParameterSpec(128, Arrays.copyOfRange(bytes, 1, 13)));
        cipher.updateAAD(ALIAS.getBytes(StandardCharsets.UTF_8));
        byte[] plain = cipher.doFinal(bytes, 13, bytes.length - 13);
        try { return new String(plain, StandardCharsets.UTF_8); }
        finally { Arrays.fill(plain, (byte) 0); }
    }

    public static synchronized void save(Context context, String json) throws Exception {
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.ENCRYPT_MODE, key(true));
        cipher.updateAAD(ALIAS.getBytes(StandardCharsets.UTF_8));
        byte[] plain = json.getBytes(StandardCharsets.UTF_8);
        byte[] encrypted;
        try { encrypted = cipher.doFinal(plain); }
        finally { Arrays.fill(plain, (byte) 0); }
        if (cipher.getIV().length != 12) throw new IllegalStateException("Invalid encryption nonce");
        AtomicFile storage = file(context);
        FileOutputStream stream = storage.startWrite();
        try {
            stream.write(1);
            stream.write(cipher.getIV());
            stream.write(encrypted);
            storage.finishWrite(stream);
        } catch (Exception error) {
            storage.failWrite(stream);
            throw error;
        }
    }

    public static synchronized void clear(Context context) throws Exception {
        try { file(context).delete(); }
        finally { keyStore().deleteEntry(ALIAS); }
    }
}
