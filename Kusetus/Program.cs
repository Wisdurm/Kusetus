using SDL2;
using System.Drawing;

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
    struct TextRender(nint texture, string value, SDL.SDL_Rect dstrect) //Used for caching text rendering
    {
        public nint texture = texture; //Render of text
        public string value = value; //Value of text rendered
        public SDL.SDL_Rect dstrect = dstrect; //Might aswell store this too
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
        static nint cardsTexture;
        static nint buttonsTexture;
        static readonly Dictionary<string, TextRender> cachedText = []; //This stores all text renders, so that they are reused as much as possible
        //Fonts
        static nint mainFont;
        //Game
        readonly static List<Card> drawPile = []; //Nostopakka
        readonly static List<Card> discardPile = []; //Kaatopakka
        readonly static List<Card> mainPile = []; //Pää korttikasa
        readonly static List<Card> deck = []; //Omat kortit

        static Card? hoverCard = null; //Card in deck hovering over
        static Card? heldCard = null; //Card being dragged by mouse

        //The size of the cards when they are rendered. Bit of a strange thing to put here but here it works best.
        const int cardWidth = 150;
        const int cardHeight = (int)(cardWidth * 1.452);

        readonly static Rectangle playArea = new((gameWidth / 2) - (cardWidth / 2), (gameHeight / 2) - (cardHeight / 2), cardWidth, cardHeight); //Area where cards are placed

        static int playedTurn = 0; //How many cards you've played this turn

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
            //ttf
            SDL_ttf.TTF_Init();
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
            LoadTexture("data/cards.png", ref cardsTexture); //Cards 500x726
            LoadTexture("data/buttonSheet.png", ref buttonsTexture); //Buttons 50x50 maybe idk
            //Load fonts
            mainFont = SDL_ttf.TTF_OpenFont("data/RawonDemo.ttf", 50);

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
                                            else if (mainPile.Count > 0 && playArea.Contains(mouseX, mouseY)) //Pick up from the pile
                                            {
                                                heldCard = mainPile.Last();
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
                                            if (playArea.Contains(mouseX, mouseY)) //Play card
                                            {
                                                if (heldCard != null)
                                                {
                                                    mainPile.Add(heldCard.Value);
                                                    deck.Remove(heldCard.Value);

                                                    playedTurn++;
                                                }
                                            }
                                            else if (mouseY > gameHeight - cardHeight && mainPile.Count > 0 && mainPile.Last().Equals(heldCard)) //Take back card
                                            {
                                                deck.Add(mainPile.Last());
                                                mainPile.Remove(mainPile.Last());
                                                playedTurn--;
                                            }
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
            //Optional cleanup (in case SDL forgets or something)
            SDL.SDL_DestroyTexture(cardsTexture);
            foreach (TextRender textRender in cachedText.Values) //Clear cache
            {
                SDL.SDL_DestroyTexture(textRender.texture);
            }
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

            //Pile
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.SDL_Rect playAreaSDL = playArea.ToSDLrect();
            SDL.SDL_RenderDrawRect(renderer, ref playAreaSDL);

            if (mainPile.Count > 0)
            {
                //Card topCard = mainPile.Last();
                if (!(mainPile.Last().Equals(heldCard) && mainPile.Count == 1)) //Basically if we're holding the only card of the mainPile, then don't render cards there
                {
                    RenderCard(playArea.X, playArea.Y, cardWidth, 15, (Suit)2);
                }
            }

            //Deck
            hoverCard = null;
            for (int i = 0; i < deck.Count; i++)
            {
                if (heldCard == null || (heldCard != null && !heldCard.Value.Equals(deck[i]))) //Sometimes I really fucking hate microsoft
                {
                    int x = (cardWidth * i) + ((gameWidth - (cardWidth * deck.Count)) / 2); //Where to render
                    int nextX = (cardWidth * (i + 1)) + ((gameWidth - (cardWidth * deck.Count)) / 2); //Where next card
                    int y = gameHeight - cardHeight;

                    if (mouseY > y && mouseX > x && mouseX < nextX)
                    {
                        RenderCard(x, y - 20, cardWidth, deck[i].value, deck[i].suit);
                        hoverCard = deck[i];
                    }
                    else
                    {
                        RenderCard(x, y, cardWidth, deck[i].value, deck[i].suit);
                    }
                }
            }
            if (heldCard != null)
            {
                RenderCard(mouseX - (cardWidth / 2), mouseY - (int)((cardWidth * 1.452) / 2), cardWidth, 15, (Suit)2);
            }
            //Other UI
            if (playedTurn > 0) //End turn UI
            {
                RenderText("playPrompt", "What card(s) did you play?", playArea.X + cardWidth / 2, playArea.Y - 100, mainFont);
                for (int i = 2; i < 15; i++)
                {
                    int buttonSize = 50;

                    SDL.SDL_Rect srcrect = new()
                    {
                        x = 0,
                        y = (i - 2) * 50,
                        w = buttonSize,
                        h = buttonSize
                    };
                    SDL.SDL_Rect dstrect = new()
                    {
                        x = ((gameWidth / 2) - ((buttonSize * 14)/2)) + ((i - 2) * buttonSize),
                        y = playArea.Y - 60,
                        w = buttonSize,
                        h = buttonSize
                    };

                    if (dstrect.ToRect().Contains(mouseX, mouseY)) //Enlarge on hover
                    {
                        dstrect.w += 6;
                        dstrect.h += 6;
                        dstrect.x -= 3;
                        dstrect.y -= 3;
                    }
                    SDL.SDL_RenderCopy(renderer, buttonsTexture, ref srcrect, ref dstrect);
                }
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
            SDL.SDL_RenderCopy(renderer, cardsTexture, ref srcrect, ref dstrect);
        }
        static SDL.SDL_Rect ToSDLrect(this Rectangle rectangle)
        {
            return new()
            {
                x = rectangle.X,
                y = rectangle.Y,
                w = rectangle.Width,
                h = rectangle.Height
            };
        }
        static Rectangle ToRect(this SDL.SDL_Rect rect)
        {
            return new()
            {
                X = rect.x,
                Y = rect.y,
                Width = rect.w,
                Height = rect.h
            };
        }
        /// <summary>
        /// Render text
        /// </summary>
        public static void RenderText(string key, string text, int x, int y, nint Font)
        {
            //This code is some months old. I've reused it for 2 projects already, even though I'm pretty sure there's still improvements to be made.

            if (text.Contains('\n'))
            {
                string[] strings = text.Split('\n');
                for (int i = 0; i < strings.Length; i++)
                {
                    RenderText($"{key}{i}", strings[i].Replace("\n", null), x, y + (i * 15), Font);
                }
            }
            else
            {
                bool containsKey = cachedText.TryGetValue(key, out TextRender textRender);
                if (!containsKey || textRender.value != text) //We need to render the text
                {
                    SDL.SDL_Color white = new();
                    white.r = white.g = white.b = white.a = 0; //Color

                    nint messageSurface = SDL_ttf.TTF_RenderText_Solid(Font, text, white);
                    //Render to texture
                    nint messageTexture = SDL.SDL_CreateTextureFromSurface(renderer, messageSurface);
                    SDL_ttf.TTF_SizeText(Font, text, out int w, out int h); //How long will the string be once rendered?
                    var messageRect = new SDL.SDL_Rect
                    {
                        x = x - w / 2,
                        y = y - h / 2,
                        w = w,
                        h = h,
                    };
                    SDL.SDL_RenderCopy(renderer, messageTexture, (nint)null, ref messageRect);
                    SDL.SDL_FreeSurface(messageSurface);
                    //Cache
                    //TODO: This could probably be improved
                    if (containsKey) //Delete old one
                    {
                        SDL.SDL_DestroyTexture(textRender.texture);
                        cachedText.Remove(key);
                    }
                    //Add new one
                    cachedText.Add(key, new TextRender(messageTexture, text, messageRect));
                }
                else //If this exact string has already been rendered, lets just use that
                {
                    if (textRender.dstrect.x != x || textRender.dstrect.y != y) //If x has changed
                    {
                        cachedText.Remove(key);
                        var dstrect = new SDL.SDL_Rect
                        {
                            x = x - textRender.dstrect.w / 2,
                            y = y - textRender.dstrect.h / 2,
                            w = textRender.dstrect.w,
                            h = textRender.dstrect.h,
                        };
                        //We stil reuse the actual render, we just change the position
                        cachedText.Add(key, new TextRender(textRender.texture, text, dstrect));
                    }
                    SDL.SDL_RenderCopy(renderer, textRender.texture, (nint)null, ref textRender.dstrect);
                }
            }
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
