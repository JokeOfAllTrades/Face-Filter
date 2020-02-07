using System;

using UnityEditor.Media;
using UnityEngine;
using Unity.Collections;
using System.IO;
using System.Collections.Generic;


namespace JokeOfAllTrades.FaceFilter.Primary
{
    public class Recorder
    {
    
        static public VideoTrackAttributes videoAttrs = new VideoTrackAttributes
        {
            frameRate = new MediaRational(50),
            width = 400,
            height = 300,
            includeAlpha = false
        };

        static public AudioTrackAttributes audioAttrs = new AudioTrackAttributes
        {
            sampleRate = new MediaRational(48000),
            channelCount = 2,
            language = "en"
        };
        
        static String encodedFilePath = Path.Combine(".\\", "my_movie.mp4");
        private int sampleFramesPerVideoFrame;
        private MediaEncoder encoder;
        private AudioClip sounds;
        Texture2D tex;
        public List<Texture2D> images;

        public Recorder()
        { 
            sampleFramesPerVideoFrame = audioAttrs.channelCount * 
                audioAttrs.sampleRate.numerator / videoAttrs.frameRate.numerator;
            sounds = Microphone.Start("", false, 200, 48000);
            images = new List<Texture2D>();
            tex = new Texture2D((int)videoAttrs.width, (int)videoAttrs.height, TextureFormat.RGBA32, false);
        }

        public void MakeVideo()
        {
            float[] soundArray;
            sounds.GetData(soundArray = new float[sounds.samples], 0);
            using (var encoder = new MediaEncoder(encodedFilePath, videoAttrs, audioAttrs))
            using (var audioBuffer = new NativeArray<float>(sampleFramesPerVideoFrame, Allocator.Temp))
            {
                sounds.GetData(audioBuffer.ToArray(), 0);
                for (int i = 0; i < images.Count; ++i)
                {
                    //tex.SetPixels(images[i].GetPixels());
                    encoder.AddFrame(images[i]);
                    encoder.AddSamples(audioBuffer);
                }
            }
            Microphone.End("");
        }
    }
}