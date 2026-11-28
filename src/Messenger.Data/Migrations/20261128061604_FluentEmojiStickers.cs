using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Messenger.Data.Migrations
{
    /// <inheritdoc />
    public partial class FluentEmojiStickers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "name", "thumbnail_path" },
                values: new object[] { "smileys", "/stickers/fluent/grinning-face.webp" });

            migrationBuilder.InsertData(
                table: "sticker_packs",
                columns: new[] { "id", "name", "thumbnail_path" },
                values: new object[,]
                {
                    { 2, "faces", "/stickers/fluent/thinking-face.webp" },
                    { 3, "feelings", "/stickers/fluent/loudly-crying-face.webp" },
                    { 4, "monsters", "/stickers/fluent/smiling-face-with-horns.webp" },
                    { 5, "animals", "/stickers/fluent/grinning-cat.webp" },
                    { 6, "hearts", "/stickers/fluent/red-heart.webp" }
                });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "code", "image_path" },
                values: new object[] { "grinning-face", "/stickers/fluent/grinning-face.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "code", "image_path" },
                values: new object[] { "beaming-face-with-smiling-eyes", "/stickers/fluent/beaming-face-with-smiling-eyes.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "code", "image_path" },
                values: new object[] { "face-with-tears-of-joy", "/stickers/fluent/face-with-tears-of-joy.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "code", "image_path" },
                values: new object[] { "grinning-face-with-big-eyes", "/stickers/fluent/grinning-face-with-big-eyes.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "code", "image_path" },
                values: new object[] { "grinning-face-with-smiling-eyes", "/stickers/fluent/grinning-face-with-smiling-eyes.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "code", "image_path" },
                values: new object[] { "grinning-face-with-sweat", "/stickers/fluent/grinning-face-with-sweat.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "code", "image_path" },
                values: new object[] { "grinning-squinting-face", "/stickers/fluent/grinning-squinting-face.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "code", "image_path" },
                values: new object[] { "smiling-face-with-halo", "/stickers/fluent/smiling-face-with-halo.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "code", "image_path" },
                values: new object[] { "winking-face", "/stickers/fluent/winking-face.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "code", "image_path" },
                values: new object[] { "smiling-face-with-smiling-eyes", "/stickers/fluent/smiling-face-with-smiling-eyes.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "code", "image_path" },
                values: new object[] { "face-savoring-food", "/stickers/fluent/face-savoring-food.webp" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "code", "image_path" },
                values: new object[] { "smiling-face-with-heart-eyes", "/stickers/fluent/smiling-face-with-heart-eyes.webp" });

            migrationBuilder.InsertData(
                table: "stickers",
                columns: new[] { "id", "code", "image_path", "pack_id" },
                values: new object[,]
                {
                    { 13, "kissing-face", "/stickers/fluent/kissing-face.webp", 1 },
                    { 14, "face-blowing-a-kiss", "/stickers/fluent/face-blowing-a-kiss.webp", 1 },
                    { 15, "kissing-face-with-smiling-eyes", "/stickers/fluent/kissing-face-with-smiling-eyes.webp", 1 },
                    { 16, "kissing-face-with-closed-eyes", "/stickers/fluent/kissing-face-with-closed-eyes.webp", 1 },
                    { 17, "face-with-tongue", "/stickers/fluent/face-with-tongue.webp", 1 },
                    { 18, "winking-face-with-tongue", "/stickers/fluent/winking-face-with-tongue.webp", 1 },
                    { 19, "squinting-face-with-tongue", "/stickers/fluent/squinting-face-with-tongue.webp", 1 },
                    { 20, "slightly-smiling-face", "/stickers/fluent/slightly-smiling-face.webp", 1 },
                    { 21, "upside-down-face", "/stickers/fluent/upside-down-face.webp", 1 },
                    { 22, "money-mouth-face", "/stickers/fluent/money-mouth-face.webp", 1 },
                    { 23, "rolling-on-the-floor-laughing", "/stickers/fluent/rolling-on-the-floor-laughing.webp", 1 },
                    { 24, "star-struck", "/stickers/fluent/star-struck.webp", 1 },
                    { 25, "zany-face", "/stickers/fluent/zany-face.webp", 1 },
                    { 26, "smiling-face-with-hearts", "/stickers/fluent/smiling-face-with-hearts.webp", 1 },
                    { 27, "smiling-face-with-tear", "/stickers/fluent/smiling-face-with-tear.webp", 1 },
                    { 28, "melting-face", "/stickers/fluent/melting-face.webp", 1 },
                    { 29, "smiling-face", "/stickers/fluent/smiling-face.webp", 1 },
                    { 30, "hugging-face", "/stickers/fluent/hugging-face.webp", 2 },
                    { 31, "face-with-hand-over-mouth", "/stickers/fluent/face-with-hand-over-mouth.webp", 2 },
                    { 32, "face-with-open-eyes-and-hand-over-mouth", "/stickers/fluent/face-with-open-eyes-and-hand-over-mouth.webp", 2 },
                    { 33, "face-with-peeking-eye", "/stickers/fluent/face-with-peeking-eye.webp", 2 },
                    { 34, "shushing-face", "/stickers/fluent/shushing-face.webp", 2 },
                    { 35, "thinking-face", "/stickers/fluent/thinking-face.webp", 2 },
                    { 36, "saluting-face", "/stickers/fluent/saluting-face.webp", 2 },
                    { 37, "zipper-mouth-face", "/stickers/fluent/zipper-mouth-face.webp", 2 },
                    { 38, "face-with-raised-eyebrow", "/stickers/fluent/face-with-raised-eyebrow.webp", 2 },
                    { 39, "neutral-face", "/stickers/fluent/neutral-face.webp", 2 },
                    { 40, "expressionless-face", "/stickers/fluent/expressionless-face.webp", 2 },
                    { 41, "face-without-mouth", "/stickers/fluent/face-without-mouth.webp", 2 },
                    { 42, "dotted-line-face", "/stickers/fluent/dotted-line-face.webp", 2 },
                    { 43, "face-in-clouds", "/stickers/fluent/face-in-clouds.webp", 2 },
                    { 44, "smirking-face", "/stickers/fluent/smirking-face.webp", 2 },
                    { 45, "unamused-face", "/stickers/fluent/unamused-face.webp", 2 },
                    { 46, "face-with-rolling-eyes", "/stickers/fluent/face-with-rolling-eyes.webp", 2 },
                    { 47, "grimacing-face", "/stickers/fluent/grimacing-face.webp", 2 },
                    { 48, "face-exhaling", "/stickers/fluent/face-exhaling.webp", 2 },
                    { 49, "lying-face", "/stickers/fluent/lying-face.webp", 2 },
                    { 50, "shaking-face", "/stickers/fluent/shaking-face.webp", 2 },
                    { 51, "head-shaking-horizontally", "/stickers/fluent/head-shaking-horizontally.webp", 2 },
                    { 52, "head-shaking-vertically", "/stickers/fluent/head-shaking-vertically.webp", 2 },
                    { 53, "relieved-face", "/stickers/fluent/relieved-face.webp", 2 },
                    { 54, "pensive-face", "/stickers/fluent/pensive-face.webp", 2 },
                    { 55, "sleepy-face", "/stickers/fluent/sleepy-face.webp", 2 },
                    { 56, "drooling-face", "/stickers/fluent/drooling-face.webp", 2 },
                    { 57, "sleeping-face", "/stickers/fluent/sleeping-face.webp", 2 },
                    { 58, "cowboy-hat-face", "/stickers/fluent/cowboy-hat-face.webp", 2 },
                    { 59, "partying-face", "/stickers/fluent/partying-face.webp", 2 },
                    { 60, "disguised-face", "/stickers/fluent/disguised-face.webp", 2 },
                    { 61, "smiling-face-with-sunglasses", "/stickers/fluent/smiling-face-with-sunglasses.webp", 2 },
                    { 62, "nerd-face", "/stickers/fluent/nerd-face.webp", 2 },
                    { 63, "face-with-monocle", "/stickers/fluent/face-with-monocle.webp", 2 },
                    { 64, "face-with-diagonal-mouth", "/stickers/fluent/face-with-diagonal-mouth.webp", 2 },
                    { 65, "face-with-medical-mask", "/stickers/fluent/face-with-medical-mask.webp", 3 },
                    { 66, "face-with-thermometer", "/stickers/fluent/face-with-thermometer.webp", 3 },
                    { 67, "face-with-head-bandage", "/stickers/fluent/face-with-head-bandage.webp", 3 },
                    { 68, "nauseated-face", "/stickers/fluent/nauseated-face.webp", 3 },
                    { 69, "face-vomiting", "/stickers/fluent/face-vomiting.webp", 3 },
                    { 70, "sneezing-face", "/stickers/fluent/sneezing-face.webp", 3 },
                    { 71, "hot-face", "/stickers/fluent/hot-face.webp", 3 },
                    { 72, "cold-face", "/stickers/fluent/cold-face.webp", 3 },
                    { 73, "woozy-face", "/stickers/fluent/woozy-face.webp", 3 },
                    { 74, "knocked-out-face", "/stickers/fluent/knocked-out-face.webp", 3 },
                    { 75, "face-with-spiral-eyes", "/stickers/fluent/face-with-spiral-eyes.webp", 3 },
                    { 76, "exploding-head", "/stickers/fluent/exploding-head.webp", 3 },
                    { 77, "confused-face", "/stickers/fluent/confused-face.webp", 3 },
                    { 78, "worried-face", "/stickers/fluent/worried-face.webp", 3 },
                    { 79, "slightly-frowning-face", "/stickers/fluent/slightly-frowning-face.webp", 3 },
                    { 80, "frowning-face", "/stickers/fluent/frowning-face.webp", 3 },
                    { 81, "face-with-open-mouth", "/stickers/fluent/face-with-open-mouth.webp", 3 },
                    { 82, "hushed-face", "/stickers/fluent/hushed-face.webp", 3 },
                    { 83, "astonished-face", "/stickers/fluent/astonished-face.webp", 3 },
                    { 84, "flushed-face", "/stickers/fluent/flushed-face.webp", 3 },
                    { 85, "pleading-face", "/stickers/fluent/pleading-face.webp", 3 },
                    { 86, "face-holding-back-tears", "/stickers/fluent/face-holding-back-tears.webp", 3 },
                    { 87, "frowning-face-with-open-mouth", "/stickers/fluent/frowning-face-with-open-mouth.webp", 3 },
                    { 88, "anguished-face", "/stickers/fluent/anguished-face.webp", 3 },
                    { 89, "fearful-face", "/stickers/fluent/fearful-face.webp", 3 },
                    { 90, "anxious-face-with-sweat", "/stickers/fluent/anxious-face-with-sweat.webp", 3 },
                    { 91, "sad-but-relieved-face", "/stickers/fluent/sad-but-relieved-face.webp", 3 },
                    { 92, "crying-face", "/stickers/fluent/crying-face.webp", 3 },
                    { 93, "loudly-crying-face", "/stickers/fluent/loudly-crying-face.webp", 3 },
                    { 94, "face-screaming-in-fear", "/stickers/fluent/face-screaming-in-fear.webp", 3 },
                    { 95, "confounded-face", "/stickers/fluent/confounded-face.webp", 3 },
                    { 96, "persevering-face", "/stickers/fluent/persevering-face.webp", 3 },
                    { 97, "disappointed-face", "/stickers/fluent/disappointed-face.webp", 3 },
                    { 98, "downcast-face-with-sweat", "/stickers/fluent/downcast-face-with-sweat.webp", 3 },
                    { 99, "weary-face", "/stickers/fluent/weary-face.webp", 3 },
                    { 100, "tired-face", "/stickers/fluent/tired-face.webp", 3 },
                    { 101, "yawning-face", "/stickers/fluent/yawning-face.webp", 3 },
                    { 102, "face-with-steam-from-nose", "/stickers/fluent/face-with-steam-from-nose.webp", 3 },
                    { 103, "pouting-face", "/stickers/fluent/pouting-face.webp", 3 },
                    { 104, "angry-face", "/stickers/fluent/angry-face.webp", 3 },
                    { 105, "face-with-symbols-on-mouth", "/stickers/fluent/face-with-symbols-on-mouth.webp", 3 },
                    { 106, "smiling-face-with-horns", "/stickers/fluent/smiling-face-with-horns.webp", 4 },
                    { 107, "angry-face-with-horns", "/stickers/fluent/angry-face-with-horns.webp", 4 },
                    { 108, "skull", "/stickers/fluent/skull.webp", 4 },
                    { 109, "skull-and-crossbones", "/stickers/fluent/skull-and-crossbones.webp", 4 },
                    { 110, "pile-of-poo", "/stickers/fluent/pile-of-poo.webp", 4 },
                    { 111, "clown-face", "/stickers/fluent/clown-face.webp", 4 },
                    { 112, "ogre", "/stickers/fluent/ogre.webp", 4 },
                    { 113, "goblin", "/stickers/fluent/goblin.webp", 4 },
                    { 114, "ghost", "/stickers/fluent/ghost.webp", 4 },
                    { 115, "alien", "/stickers/fluent/alien.webp", 4 },
                    { 116, "alien-monster", "/stickers/fluent/alien-monster.webp", 4 },
                    { 117, "robot", "/stickers/fluent/robot.webp", 4 },
                    { 118, "grinning-cat", "/stickers/fluent/grinning-cat.webp", 5 },
                    { 119, "grinning-cat-with-smiling-eyes", "/stickers/fluent/grinning-cat-with-smiling-eyes.webp", 5 },
                    { 120, "cat-with-tears-of-joy", "/stickers/fluent/cat-with-tears-of-joy.webp", 5 },
                    { 121, "smiling-cat-with-heart-eyes", "/stickers/fluent/smiling-cat-with-heart-eyes.webp", 5 },
                    { 122, "cat-with-wry-smile", "/stickers/fluent/cat-with-wry-smile.webp", 5 },
                    { 123, "kissing-cat", "/stickers/fluent/kissing-cat.webp", 5 },
                    { 124, "weary-cat", "/stickers/fluent/weary-cat.webp", 5 },
                    { 125, "crying-cat", "/stickers/fluent/crying-cat.webp", 5 },
                    { 126, "pouting-cat", "/stickers/fluent/pouting-cat.webp", 5 },
                    { 127, "see-no-evil-monkey", "/stickers/fluent/see-no-evil-monkey.webp", 5 },
                    { 128, "hear-no-evil-monkey", "/stickers/fluent/hear-no-evil-monkey.webp", 5 },
                    { 129, "speak-no-evil-monkey", "/stickers/fluent/speak-no-evil-monkey.webp", 5 },
                    { 130, "love-letter", "/stickers/fluent/love-letter.webp", 6 },
                    { 131, "heart-with-arrow", "/stickers/fluent/heart-with-arrow.webp", 6 },
                    { 132, "heart-with-ribbon", "/stickers/fluent/heart-with-ribbon.webp", 6 },
                    { 133, "sparkling-heart", "/stickers/fluent/sparkling-heart.webp", 6 },
                    { 134, "growing-heart", "/stickers/fluent/growing-heart.webp", 6 },
                    { 135, "beating-heart", "/stickers/fluent/beating-heart.webp", 6 },
                    { 136, "revolving-hearts", "/stickers/fluent/revolving-hearts.webp", 6 },
                    { 137, "two-hearts", "/stickers/fluent/two-hearts.webp", 6 },
                    { 138, "heart-decoration", "/stickers/fluent/heart-decoration.webp", 6 },
                    { 139, "heart-exclamation", "/stickers/fluent/heart-exclamation.webp", 6 },
                    { 140, "broken-heart", "/stickers/fluent/broken-heart.webp", 6 },
                    { 141, "heart-on-fire", "/stickers/fluent/heart-on-fire.webp", 6 },
                    { 142, "mending-heart", "/stickers/fluent/mending-heart.webp", 6 },
                    { 143, "red-heart", "/stickers/fluent/red-heart.webp", 6 },
                    { 144, "pink-heart", "/stickers/fluent/pink-heart.webp", 6 },
                    { 145, "orange-heart", "/stickers/fluent/orange-heart.webp", 6 },
                    { 146, "yellow-heart", "/stickers/fluent/yellow-heart.webp", 6 },
                    { 147, "green-heart", "/stickers/fluent/green-heart.webp", 6 },
                    { 148, "blue-heart", "/stickers/fluent/blue-heart.webp", 6 },
                    { 149, "light-blue-heart", "/stickers/fluent/light-blue-heart.webp", 6 },
                    { 150, "purple-heart", "/stickers/fluent/purple-heart.webp", 6 },
                    { 151, "brown-heart", "/stickers/fluent/brown-heart.webp", 6 },
                    { 152, "black-heart", "/stickers/fluent/black-heart.webp", 6 },
                    { 153, "grey-heart", "/stickers/fluent/grey-heart.webp", 6 },
                    { 154, "white-heart", "/stickers/fluent/white-heart.webp", 6 },
                    { 155, "kiss-mark", "/stickers/fluent/kiss-mark.webp", 6 },
                    { 156, "hundred-points", "/stickers/fluent/hundred-points.webp", 6 },
                    { 157, "anger-symbol", "/stickers/fluent/anger-symbol.webp", 6 },
                    { 158, "collision", "/stickers/fluent/collision.webp", 6 },
                    { 159, "dizzy", "/stickers/fluent/dizzy.webp", 6 },
                    { 160, "sweat-droplets", "/stickers/fluent/sweat-droplets.webp", 6 },
                    { 161, "dashing-away", "/stickers/fluent/dashing-away.webp", 6 },
                    { 162, "hole", "/stickers/fluent/hole.webp", 6 },
                    { 163, "bomb", "/stickers/fluent/bomb.webp", 6 },
                    { 164, "speech-balloon", "/stickers/fluent/speech-balloon.webp", 6 },
                    { 165, "eye-in-speech-bubble", "/stickers/fluent/eye-in-speech-bubble.webp", 6 },
                    { 166, "left-speech-bubble", "/stickers/fluent/left-speech-bubble.webp", 6 },
                    { 167, "right-anger-bubble", "/stickers/fluent/right-anger-bubble.webp", 6 },
                    { 168, "thought-balloon", "/stickers/fluent/thought-balloon.webp", 6 },
                    { 169, "zzz", "/stickers/fluent/zzz.webp", 6 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.UpdateData(
                table: "sticker_packs",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "name", "thumbnail_path" },
                values: new object[] { "basic", "/stickers/basic/hello.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "code", "image_path" },
                values: new object[] { "hello", "/stickers/basic/hello.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "code", "image_path" },
                values: new object[] { "thanks", "/stickers/basic/thanks.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "code", "image_path" },
                values: new object[] { "love", "/stickers/basic/love.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "code", "image_path" },
                values: new object[] { "laugh", "/stickers/basic/laugh.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "code", "image_path" },
                values: new object[] { "cry", "/stickers/basic/cry.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "code", "image_path" },
                values: new object[] { "ok", "/stickers/basic/ok.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "code", "image_path" },
                values: new object[] { "sorry", "/stickers/basic/sorry.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "code", "image_path" },
                values: new object[] { "wow", "/stickers/basic/wow.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "code", "image_path" },
                values: new object[] { "angry", "/stickers/basic/angry.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "code", "image_path" },
                values: new object[] { "sleepy", "/stickers/basic/sleepy.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "code", "image_path" },
                values: new object[] { "yes", "/stickers/basic/yes.svg" });

            migrationBuilder.UpdateData(
                table: "stickers",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "code", "image_path" },
                values: new object[] { "no", "/stickers/basic/no.svg" });
        }
    }
}
