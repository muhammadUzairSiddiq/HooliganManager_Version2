package com.hooliganmanager.voice;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.os.Bundle;
import android.speech.RecognizerIntent;
import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.Locale;

/** Transparent bridge to the device speech-recognition provider. */
public final class VoiceRecognitionActivity extends Activity {
    private static final int REQUEST_SPEECH = 7304;

    public static void start(Activity activity) {
        activity.startActivity(new Intent(activity, VoiceRecognitionActivity.class));
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, Locale.getDefault());
        intent.putExtra(RecognizerIntent.EXTRA_PROMPT, "Ask the pedestrian a question");
        try { startActivityForResult(intent, REQUEST_SPEECH); }
        catch (ActivityNotFoundException error) { send("__CANCELLED__"); finish(); }
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        String phrase = "__CANCELLED__";
        if (requestCode == REQUEST_SPEECH && resultCode == RESULT_OK && data != null) {
            ArrayList<String> results = data.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS);
            if (results != null && !results.isEmpty()) phrase = results.get(0);
        }
        send(phrase);
        finish();
    }

    private static void send(String phrase) {
        try {
            Class<?> unityPlayer = Class.forName("com.unity3d.player.UnityPlayer");
            Method sendMessage = unityPlayer.getMethod("UnitySendMessage", String.class, String.class, String.class);
            sendMessage.invoke(null, "NpcConversationUI", "OnVoiceResult", phrase == null ? "__CANCELLED__" : phrase);
        } catch (Exception ignored) { }
    }
}
