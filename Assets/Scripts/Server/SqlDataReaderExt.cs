// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Data.SqlClient;
using Imma.Common;

namespace Imma.Foobbuteo.Server
{
    public static class SqlDataReaderExt
    {
        public static string GetString(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => r.GetString(r.GetOrdinal(key)));

        public static int GetInt32(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => r.GetInt32(r.GetOrdinal(key)));

        public static byte GetByte(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => r.GetByte(r.GetOrdinal(key)));

        public static int GetSkill(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => (int)Math.Round(r.GetDouble(r.GetOrdinal(key))));

        public static float GetFloat(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => r.GetFloat(r.GetOrdinal(key)));

        public static bool GetBoolean(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => r.GetBoolean(r.GetOrdinal(key)));

        public static DateTime GetDateTime(this SqlDataReader r, string key) =>
            r.GetSafe(key, (r) => r.GetDateTime(r.GetOrdinal(key)));

        public static bool IsDBNull(this SqlDataReader r, string key) => r.IsDBNull(r.GetOrdinal(key));

        public static T GetSafe<T>(this SqlDataReader r, string key, Func<SqlDataReader, T> fn)
        {
            try
            {
                return fn(r);
            }
            catch (IndexOutOfRangeException ex)
            {
                ImmLogger.LError<SqlDataReader>($"key: {key}; {ex.GetType().Name}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                object value = null;
                string dataTye = null;
                try
                {
                    dataTye = r.GetDataTypeName(r.GetOrdinal(key));
                    value = r.GetValue(r.GetOrdinal(key));
                }
                catch
                {
                    // This is intentionally left empty.
                }

                ImmLogger.LError<SqlDataReader>(
                    $"key: {key}, sqlType: {dataTye}, value: {value}, type: {value?.GetType()}; {ex.GetType().Name}: {ex.Message}"
                );

                throw;
            }
        }
    }
}
