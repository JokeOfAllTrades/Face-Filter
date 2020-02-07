namespace Evereal.VideoCapture
{
  public class MathUtils
  {
    public static bool CheckPowerOfTwo(int input)
    {
      return (input & (input - 1)) == 0;
    }
  }
}