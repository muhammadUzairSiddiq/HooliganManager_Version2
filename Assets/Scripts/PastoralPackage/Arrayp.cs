using System;
using UnityEngine;

namespace Pastoral
{
    public static class Arrayp
    {
        public static byte[] FillArrayWithIndex(byte size)
        {
            byte[] array = new byte[size];
            for (byte i = 0; i < size; i++)
            {
                array[i] = i;
            }
            return array;
        }
        public static int[] FillArrayWithIndex(int size)
        {
            int[] array = new int[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = i;
            }
            return array;
        }
    }
}