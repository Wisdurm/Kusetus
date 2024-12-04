using SDL2;

namespace Kusetus
{
    enum Suit
    {
        Club,
        Diamond,
        Heart,
        Spade
    }
    readonly struct Card(Suit suit, int value)
    {
        public readonly Suit suit = suit;
        public readonly int value = value;
    }
    static class Program
    {
        //Window
        static nint window;
        static readonly int windowWidth = 1000;
        static readonly int windowHeight = 800;
        //Render
        static nint renderer;
        static nint render;
        //Engine
        static readonly int gameWidth = 1000;
        static readonly int gameHeight = 800;
        static readonly Random random = new();
        //Textures
        static nint cards;
        //Game
        readonly static List<Card> drawPile = [];
        //readonly static List<Card>
        readonly static List<Card> deck = [];

        static Card? hoverCard = null; //Card in deck hovering over
        static Card? heldCard = null; //Card being dragged by mouse

        //*Input
        static int mouseX;
        static int mouseY;
        static bool mouseDown = false;

        static void Main()
        {
            //Initialize SDL
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) != 0)
            {
                Console.WriteLine($"SDL not initialized. Error {SDL.SDL_GetError()}");
            }
            //SDl_image
            SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);
            //Create 
            window = SDL.SDL_CreateWindow("Kusetus", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, windowWidth, windowHeight, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (window == IntPtr.Zero)
            {
                Console.WriteLine($"Unable to create window. Error {SDL.SDL_GetError()}");
            }
            //Initialize renderer
            renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            //Set up render
            render = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGB888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, gameWidth, gameHeight); //Init render
            //Load textures
            LoadTexture("data/cards.png", ref cards); //Cards 500x726

            bool running = true;
            while (running)
            {
                //Input
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
                {
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT:
                            {
                                running = false;
                                break;
                            }
                        case SDL.SDL_EventType.SDL_KEYDOWN:
                            {
                                switch (e.key.keysym.sym)
                                {
                                    case SDL.SDL_Keycode.SDLK_SPACE:
                                        drawPile.Remove(drawPile.Last());
                                        break;
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEMOTION:
                            {
                                mouseX = (int)(e.motion.x * (gameWidth / Convert.ToDouble(windowWidth))); //Division rounding my HATED
                                mouseY = (int)(e.motion.y * (gameHeight / Convert.ToDouble(windowHeight)));
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                            {
                                switch (e.button.button)
                                {
                                    case (byte)SDL.SDL_BUTTON_LEFT:
                                        {
                                            mouseDown = true;

                                            if (hoverCard != null)
                                            {
                                                heldCard = hoverCard;
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                            {
                                switch (e.button.button)
                                {
                                    case (byte)SDL.SDL_BUTTON_LEFT:
                                        {
                                            mouseDown = false;
                                            heldCard = null;
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }
                //Post input
                Render();
                Logic();
            }
            //Optional cleanup
            SDL.SDL_DestroyTexture(cards);
        }
        static void LoadTexture(string path, ref nint textureName)
        {
            nint surface = SDL_image.IMG_Load(path);
            textureName = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            SDL.SDL_FreeSurface(surface);
        }
        static void Render()
        {
            SDL.SDL_SetRenderTarget(renderer, render);
            //Graphics

            //Clear
            SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            SDL.SDL_RenderClear(renderer);

            //Deck
            const int cardSize = 150;

            hoverCard = null;
            for (int i = 0; i < deck.Count; i++)
            {
                if (heldCard == null || (heldCard != null && !heldCard.Value.Equals(deck[i]))) //Sometimes I really fucking hate microsoft
                {
                    int x = (cardSize * i) + ((gameWidth - (cardSize * deck.Count)) / 2); //Where to render
                    int nextX = (cardSize * (i + 1)) + ((gameWidth - (cardSize * deck.Count)) / 2); //Where next card
                    int y = gameHeight - 200;

                    if (mouseY > y && mouseX > x && mouseX < nextX)
                    {
                        RenderCard(x, y - 20, cardSize, deck[i].value, deck[i].suit);
                        hoverCard = deck[i];
                    }
                    else
                    {
                        RenderCard(x, y, cardSize, deck[i].value, deck[i].suit);
                    }
                }
            }
            if (heldCard != null)
            {
                RenderCard(mouseX - (cardSize / 2), mouseY-(int)((cardSize*1.452)/2), cardSize, heldCard.Value.value, heldCard.Value.suit);
            }
            //Post graphics
            SDL.SDL_SetRenderTarget(renderer, (nint)null);
            SDL.SDL_RenderCopy(renderer, render, (nint)null, (nint)null);
            SDL.SDL_RenderPresent(renderer);
        }
        static void RenderCard(int x, int y, int size, int value, Suit suit)
        {
            SDL.SDL_Rect srcrect = new()
            {
                w = 500,
                h = 726,
                x = (int)suit * 500,
                y = (value - 2) * 726 //Minus 2 since there is no 0 or 1
            };
            SDL.SDL_Rect dstrect = new()
            {
                w = size,
                h = (int)(size * 1.452),
                x = x,
                y = y
            };
            SDL.SDL_RenderCopy(renderer, cards, ref srcrect, ref dstrect);
        }
        static void Logic()
        {
            if (drawPile.Count == 0) //Create drawpile
            {
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 2; j < 15; j++)
                    {
                        drawPile.Add(new Card((Suit)i, j));
                    }
                }
                drawPile.Shuffle();

                for (int i = 0; i < 5; i++)
                {
                    deck.Add(drawPile.Last());
                    drawPile.Remove(drawPile.Last());
                }
            }
        }
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]); //??
            }
        }
    }
}
