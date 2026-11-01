using System;
using System.IO;
using UnityEngine;

public static class LocalProfile
{
    public static string PlayerName = "Player";
    public static byte[] FaceJpg;

    static bool faceLoaded;

    public static void LoadSavedFace()
    {
        if (faceLoaded) return;

        faceLoaded = true;
        string path = GetFacePath();
        if (!File.Exists(path)) return;

        try
        {
            FaceJpg = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogError($"No se pudo cargar la foto guardada: {exception.Message}");
        }
    }

    public static bool SaveFace(byte[] jpg)
    {
        string path = GetFacePath();

        try
        {
            File.WriteAllBytes(path, jpg);
            FaceJpg = jpg;
            faceLoaded = true;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogError($"No se pudo guardar la foto: {exception.Message}");
            FaceJpg = jpg;
            faceLoaded = true;
            return false;
        }
    }

    static string GetFacePath()
    {
        return Path.Combine(Application.persistentDataPath, "face.jpg");
    }
}
