/**
 * Build process for YetAnotherForum.NET
 *
 * Don't know where to start?
 * Try: http://24ways.org/2013/grunt-is-not-weird-and-hard/
 */
module.exports = function(grunt) {
    // CONFIGURATION
    grunt.initConfig({
        pkg: grunt.file.readJSON('package.json'),
        secret: grunt.file.readJSON('secret.json'),
        curl: {
            'regexes': {
                src: 'https://raw.githubusercontent.com/ua-parser/uap-core/master/regexes.yaml',
                dest: './UAParser.Core/regexes.yaml'
            }
        },

        'curl-dir': {
            'UAParser.Tests/tests': [
                'https://raw.githubusercontent.com/ua-parser/uap-core/master/tests/regexes.js',
                'https://raw.githubusercontent.com/ua-parser/uap-core/master/tests/sample.js',
                'https://raw.githubusercontent.com/ua-parser/uap-core/master/tests/test.js',
                'https://raw.githubusercontent.com/ua-parser/uap-core/master/tests/test_device.yaml',
                'https://raw.githubusercontent.com/ua-parser/uap-core/master/tests/test_os.yaml',
                'https://raw.githubusercontent.com/ua-parser/uap-core/master/tests/test_ua.yaml'
            ]
        },

        devUpdate: {
            main: {
                options: {
                    reportUpdated: true,
                    updateType: 'force',
                    semver: false,
                    packages: {
                        devDependencies: true, //only check for devDependencies
                        dependencies: true
                    }
                }
            }
        },
        shell: {
            convertYAML: {
                command: [
                    '@echo off',
                    'cd YamlConverter\\bin\\Debug\\net9.0\\',
                    'echo convert YAML to JSON',
                    'YamlConverter'
                ].join('&&')
            },
            publishNuGet: {
                command: [
                    '@echo off',
                    'cd UAParser.Core\\bin\\Release\\',
                    'echo publish Package to NuGet',
                    'dotnet nuget push "UAParser.Core.<%= pkg.version %>.nupkg" --source "https://api.nuget.org/v3/index.json" --api-key "<%= secret.api %>"'
                ].join('&&')
            }
        }
    });

    // PLUGINS
    grunt.loadNpmTasks('@w8tcha/grunt-dev-update');
    grunt.loadNpmTasks('grunt-curl');
    grunt.loadNpmTasks('grunt-shell');

    grunt.registerTask('default',
        [
            'curl', 'curl-dir', 'shell:convertYAML'
        ]);

    grunt.registerTask('publish',
        [
            'shell:publishNuGet'
        ]);
};
